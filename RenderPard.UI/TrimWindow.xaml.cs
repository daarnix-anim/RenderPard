using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RenderPard.Core.Models;
using RenderPard.UI.ViewModels;

namespace RenderPard.UI;

public partial class TrimWindow : Window
{
    private readonly TrimViewModel _viewModel;
    private readonly DispatcherTimer _playbackTimer;
    private bool _isDraggingSlider;
    private bool _isDraggingCropBox;
    private Point _lastCropMousePosition;

    public TranscodeTask? CreatedTask { get; private set; }
    public bool StartImmediately { get; private set; }

    public TrimWindow(string filePath, Preset? initialPreset = null)
    {
        InitializeComponent();
        _viewModel = new TrimViewModel(filePath, initialPreset);
        DataContext = _viewModel;

        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        _viewModel.RequestPlay += OnRequestPlay;
        _viewModel.RequestPause += OnRequestPause;
        _viewModel.RequestSeek += OnRequestSeek;
        _viewModel.RequestExport += OnRequestExport;
        _viewModel.RequestClose += () => Close();
        _viewModel.RequestCropOverlayUpdate += UpdateCropVisuals;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Loaded += TrimWindow_Loaded;
        SizeChanged += (s, e) =>
        {
            UpdateTimelineVisuals();
            UpdateCropVisuals();
        };
    }

    private void TrimWindow_Loaded(object sender, RoutedEventArgs e)
    {
        string ext = Path.GetExtension(_viewModel.SourceFilePath).ToLower();
        bool isAudio = ext is ".mp3" or ".wav" or ".ogg" or ".opus" or ".flac" or ".aac" or ".m4a" or ".wma" or ".caf" or ".aiff";
        if (isAudio)
        {
            AudioVisualIndicator.Visibility = Visibility.Visible;
        }

        try
        {
            PlayerMediaElement.Source = new Uri(_viewModel.SourceFilePath, UriKind.Absolute);
            PlayerMediaElement.Play();
            PlayerMediaElement.Pause(); // Pre-load frame
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть медиафайл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PlayerMediaElement_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (PlayerMediaElement.NaturalDuration.HasTimeSpan)
        {
            _viewModel.TotalDurationSeconds = PlayerMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            UpdateTimelineVisuals();
        }

        if (PlayerMediaElement.NaturalVideoWidth > 0 && PlayerMediaElement.NaturalVideoHeight > 0)
        {
            _viewModel.SetSourceResolution(PlayerMediaElement.NaturalVideoWidth, PlayerMediaElement.NaturalVideoHeight);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCropVisuals));
        }
    }

    private void PlayerViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCropVisuals();
    }

    private Rect GetVideoDisplayRect()
    {
        double containerW = CropCanvas.ActualWidth;
        double containerH = CropCanvas.ActualHeight;
        if (containerW <= 0 || containerH <= 0) return Rect.Empty;

        double videoW = PlayerMediaElement.NaturalVideoWidth > 0 ? PlayerMediaElement.NaturalVideoWidth : (_viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920);
        double videoH = PlayerMediaElement.NaturalVideoHeight > 0 ? PlayerMediaElement.NaturalVideoHeight : (_viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080);

        double scale = Math.Min(containerW / videoW, containerH / videoH);
        double displayW = videoW * scale;
        double displayH = videoH * scale;
        double displayX = (containerW - displayW) / 2.0;
        double displayY = (containerH - displayH) / 2.0;

        return new Rect(displayX, displayY, displayW, displayH);
    }

    private void UpdateCropVisuals()
    {
        if (!_viewModel.IsCropActive) return;

        var vRect = GetVideoDisplayRect();
        if (vRect.IsEmpty || vRect.Width <= 0 || vRect.Height <= 0) return;

        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;

        double scaleX = vRect.Width / sw;
        double scaleY = vRect.Height / sh;

        double boxX = vRect.X + (_viewModel.CropX * scaleX);
        double boxY = vRect.Y + (_viewModel.CropY * scaleY);
        double boxW = _viewModel.CropWidth * scaleX;
        double boxH = _viewModel.CropHeight * scaleY;

        Canvas.SetLeft(CropBoxGrid, boxX);
        Canvas.SetTop(CropBoxGrid, boxY);
        CropBoxGrid.Width = Math.Max(20, boxW);
        CropBoxGrid.Height = Math.Max(20, boxH);

        double canvasW = CropCanvas.ActualWidth;
        double canvasH = CropCanvas.ActualHeight;

        // Position Top mask
        Canvas.SetLeft(MaskTop, 0);
        Canvas.SetTop(MaskTop, 0);
        MaskTop.Width = Math.Max(0, canvasW);
        MaskTop.Height = Math.Max(0, boxY);

        // Position Bottom mask
        Canvas.SetLeft(MaskBottom, 0);
        Canvas.SetTop(MaskBottom, boxY + boxH);
        MaskBottom.Width = Math.Max(0, canvasW);
        MaskBottom.Height = Math.Max(0, canvasH - (boxY + boxH));

        // Position Left mask
        Canvas.SetLeft(MaskLeft, 0);
        Canvas.SetTop(MaskLeft, boxY);
        MaskLeft.Width = Math.Max(0, boxX);
        MaskLeft.Height = Math.Max(0, boxH);

        // Position Right mask
        Canvas.SetLeft(MaskRight, boxX + boxW);
        Canvas.SetTop(MaskRight, boxY);
        MaskRight.Width = Math.Max(0, canvasW - (boxX + boxW));
        MaskRight.Height = Math.Max(0, boxH);
    }

    private void CropBoxGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCropBox = true;
        _lastCropMousePosition = e.GetPosition(CropCanvas);
        CropBoxGrid.CaptureMouse();
        e.Handled = true;
    }

    private void CropBoxGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingCropBox)
        {
            _isDraggingCropBox = false;
            CropBoxGrid.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void CropBoxGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingCropBox) return;

        Point currentPos = e.GetPosition(CropCanvas);
        double deltaX = currentPos.X - _lastCropMousePosition.X;
        double deltaY = currentPos.Y - _lastCropMousePosition.Y;
        _lastCropMousePosition = currentPos;

        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int videoDeltaX = (int)(deltaX / scale);
        int videoDeltaY = (int)(deltaY / scale);

        int newX = Math.Max(0, Math.Min(_viewModel.CropX + videoDeltaX, (int)sw - _viewModel.CropWidth));
        int newY = Math.Max(0, Math.Min(_viewModel.CropY + videoDeltaY, (int)sh - _viewModel.CropHeight));

        _viewModel.CropX = newX & ~1;
        _viewModel.CropY = newY & ~1;
        UpdateCropVisuals();
        e.Handled = true;
    }

    private void ResizeCropBox(double dLeft, double dTop, double dRight, double dBottom)
    {
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidL = (int)(dLeft / scale);
        int dVidT = (int)(dTop / scale);
        int dVidR = (int)(dRight / scale);
        int dVidB = (int)(dBottom / scale);

        int newX = _viewModel.CropX + dVidL;
        int newY = _viewModel.CropY + dVidT;
        int newW = _viewModel.CropWidth + (dVidR - dVidL);
        int newH = _viewModel.CropHeight + (dVidB - dVidT);

        // Aspect ratio locking for 9:16, 16:9, 1:1
        double? lockedRatio = _viewModel.CropMode switch
        {
            CropAspectRatioMode.Vertical9x16 => 9.0 / 16.0,
            CropAspectRatioMode.Horizontal16x9 => 16.0 / 9.0,
            CropAspectRatioMode.Square1x1 => 1.0,
            _ => null
        };

        if (lockedRatio.HasValue)
        {
            double ratio = lockedRatio.Value;
            if (dVidR != 0 || dVidL != 0)
            {
                newH = (int)(newW / ratio);
            }
            else
            {
                newW = (int)(newH * ratio);
            }
        }

        // Clamp
        const int minSize = 64;
        newW = Math.Max(minSize, Math.Min(newW, (int)sw));
        newH = Math.Max(minSize, Math.Min(newH, (int)sh));

        if (newX < 0) newX = 0;
        if (newY < 0) newY = 0;
        if (newX + newW > sw) newX = (int)sw - newW;
        if (newY + newH > sh) newY = (int)sh - newH;

        _viewModel.CropX = Math.Max(0, newX) & ~1;
        _viewModel.CropY = Math.Max(0, newY) & ~1;
        _viewModel.CropWidth = Math.Max(minSize, newW) & ~1;
        _viewModel.CropHeight = Math.Max(minSize, newH) & ~1;

        UpdateCropVisuals();
    }

    private void HandleNW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(e.HorizontalChange, e.VerticalChange, 0, 0);
    private void HandleNE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(0, e.VerticalChange, e.HorizontalChange, 0);
    private void HandleSW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(e.HorizontalChange, 0, 0, e.VerticalChange);
    private void HandleSE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(0, 0, e.HorizontalChange, e.VerticalChange);
    private void HandleN_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(0, e.VerticalChange, 0, 0);
    private void HandleS_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(0, 0, 0, e.VerticalChange);
    private void HandleW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(e.HorizontalChange, 0, 0, 0);
    private void HandleE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeCropBox(0, 0, e.HorizontalChange, 0);

    private void PlayerMediaElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPlaying = false;
        _playbackTimer.Stop();
        _viewModel.SeekTo(_viewModel.InPointSeconds ?? 0);
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDraggingSlider && PlayerMediaElement.NaturalDuration.HasTimeSpan)
        {
            double current = PlayerMediaElement.Position.TotalSeconds;
            _viewModel.CurrentTimeSeconds = current;

            // If we have reached OutPoint, loop back to InPoint
            if (_viewModel.OutPointSeconds.HasValue && current >= _viewModel.OutPointSeconds.Value)
            {
                _viewModel.SeekTo(_viewModel.InPointSeconds ?? 0);
            }
        }
    }

    private void OnRequestPlay()
    {
        PlayerMediaElement.Play();
        _playbackTimer.Start();
    }

    private void OnRequestPause()
    {
        PlayerMediaElement.Pause();
        _playbackTimer.Stop();
    }

    private void OnRequestSeek(double seconds)
    {
        PlayerMediaElement.Position = TimeSpan.FromSeconds(seconds);
    }

    private void OnRequestExport(TranscodeTask task, bool startImmediately)
    {
        CreatedTask = task;
        StartImmediately = startImmediately;
        DialogResult = true;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrimViewModel.InPointSeconds) or 
            nameof(TrimViewModel.OutPointSeconds) or 
            nameof(TrimViewModel.TotalDurationSeconds))
        {
            UpdateTimelineVisuals();
        }
    }

    private void UpdateTimelineVisuals()
    {
        double trackWidth = TimelineSlider.ActualWidth;
        if (trackWidth <= 0 || _viewModel.TotalDurationSeconds <= 0) return;

        double inFraction = _viewModel.InPointFraction;
        double outFraction = _viewModel.OutPointFraction;

        double inX = inFraction * trackWidth;
        double outX = outFraction * trackWidth;

        Canvas.SetLeft(InBracketMarker, Math.Max(0, inX - 4));
        Canvas.SetLeft(OutBracketMarker, Math.Max(0, outX - 4));

        Canvas.SetLeft(TrimHighlightBar, inX);
        TrimHighlightBar.Width = Math.Max(0, outX - inX);
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingSlider)
        {
            _viewModel.SeekTo(e.NewValue);
        }
    }

    private void TimelineSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void TimelineSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = false;
        _viewModel.SeekTo(TimelineSlider.Value);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't capture keys if user is typing in a TextBox
        if (e.OriginalSource is TextBox) return;

        if (e.Key == Key.Space)
        {
            _viewModel.TogglePlay();
            e.Handled = true;
        }
        else if (e.Key == Key.I || e.Key == Key.OemOpenBrackets)
        {
            _viewModel.SetInPoint();
            e.Handled = true;
        }
        else if (e.Key == Key.O || e.Key == Key.OemCloseBrackets)
        {
            _viewModel.SetOutPoint();
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                _viewModel.StepSecondBackward();
            else
                _viewModel.StepFrameBackward();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                _viewModel.StepSecondForward();
            else
                _viewModel.StepFrameForward();
            e.Handled = true;
        }
        else if (e.Key == Key.Home)
        {
            _viewModel.SeekTo(0);
            e.Handled = true;
        }
        else if (e.Key == Key.End)
        {
            _viewModel.SeekTo(_viewModel.TotalDurationSeconds);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _playbackTimer.Stop();
        try
        {
            PlayerMediaElement.Stop();
            PlayerMediaElement.Close();
        }
        catch { }
        base.OnClosed(e);
    }
}
