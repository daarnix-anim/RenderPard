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

    private double? GetLockedRatio()
    {
        return _viewModel.CropMode switch
        {
            CropAspectRatioMode.Vertical9x16 => 9.0 / 16.0,
            CropAspectRatioMode.Horizontal16x9 => 16.0 / 9.0,
            CropAspectRatioMode.Square1x1 => 1.0,
            _ => null
        };
    }

    private void ResizeHandle_S(double dy)
    {
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidY = (int)(dy / scale);
        const int minSize = 64;

        int top = _viewModel.CropY;
        int left = _viewModel.CropX;
        int oldH = _viewModel.CropHeight;
        int oldW = _viewModel.CropWidth;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int maxH = (int)sh - top;
            int newH = Math.Max(minSize, Math.Min(maxH, oldH + dVidY));
            int newW = (int)(newH * ratio.Value);

            if (newW > (int)sw)
            {
                newW = (int)sw;
                newH = (int)(newW / ratio.Value);
            }

            int centerX = left + oldW / 2;
            int newX = Math.Max(0, Math.Min((int)sw - newW, centerX - newW / 2));

            _viewModel.CropX = newX & ~1;
            _viewModel.CropY = top & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int maxH = (int)sh - top;
            int newH = Math.Max(minSize, Math.Min(maxH, oldH + dVidY));

            _viewModel.CropY = top & ~1;
            _viewModel.CropHeight = newH & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_N(double dy)
    {
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidY = (int)(dy / scale);
        const int minSize = 64;

        int bottom = _viewModel.CropY + _viewModel.CropHeight;
        int left = _viewModel.CropX;
        int oldW = _viewModel.CropWidth;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int maxH = bottom;
            int targetH = (_viewModel.CropHeight - dVidY);
            int newH = Math.Max(minSize, Math.Min(maxH, targetH));
            int newW = (int)(newH * ratio.Value);

            if (newW > (int)sw)
            {
                newW = (int)sw;
                newH = (int)(newW / ratio.Value);
            }

            int newY = bottom - newH;
            int centerX = left + oldW / 2;
            int newX = Math.Max(0, Math.Min((int)sw - newW, centerX - newW / 2));

            _viewModel.CropX = newX & ~1;
            _viewModel.CropY = newY & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int targetY = _viewModel.CropY + dVidY;
            int newY = Math.Max(0, Math.Min(bottom - minSize, targetY));
            int newH = bottom - newY;

            _viewModel.CropY = newY & ~1;
            _viewModel.CropHeight = newH & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_E(double dx)
    {
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidX = (int)(dx / scale);
        const int minSize = 64;

        int left = _viewModel.CropX;
        int top = _viewModel.CropY;
        int oldH = _viewModel.CropHeight;
        int oldW = _viewModel.CropWidth;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int maxW = (int)sw - left;
            int newW = Math.Max(minSize, Math.Min(maxW, oldW + dVidX));
            int newH = (int)(newW / ratio.Value);

            if (newH > (int)sh)
            {
                newH = (int)sh;
                newW = (int)(newH * ratio.Value);
            }

            int centerY = top + oldH / 2;
            int newY = Math.Max(0, Math.Min((int)sh - newH, centerY - newH / 2));

            _viewModel.CropX = left & ~1;
            _viewModel.CropY = newY & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int maxW = (int)sw - left;
            int newW = Math.Max(minSize, Math.Min(maxW, oldW + dVidX));

            _viewModel.CropX = left & ~1;
            _viewModel.CropWidth = newW & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_W(double dx)
    {
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidX = (int)(dx / scale);
        const int minSize = 64;

        int right = _viewModel.CropX + _viewModel.CropWidth;
        int top = _viewModel.CropY;
        int oldH = _viewModel.CropHeight;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int maxW = right;
            int targetW = (_viewModel.CropWidth - dVidX);
            int newW = Math.Max(minSize, Math.Min(maxW, targetW));
            int newH = (int)(newW / ratio.Value);

            if (newH > (int)sh)
            {
                newH = (int)sh;
                newW = (int)(newH * ratio.Value);
            }

            int newX = right - newW;
            int centerY = top + oldH / 2;
            int newY = Math.Max(0, Math.Min((int)sh - newH, centerY - newH / 2));

            _viewModel.CropX = newX & ~1;
            _viewModel.CropY = newY & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int targetX = _viewModel.CropX + dVidX;
            int newX = Math.Max(0, Math.Min(right - minSize, targetX));
            int newW = right - newX;

            _viewModel.CropX = newX & ~1;
            _viewModel.CropWidth = newW & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_SE(double dx, double dy)
    {
        // Anchor: Top-Left (left, top) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidX = (int)(dx / scale);
        int dVidY = (int)(dy / scale);
        const int minSize = 64;

        int left = _viewModel.CropX;
        int top = _viewModel.CropY;
        int maxW = (int)sw - left;
        int maxH = (int)sh - top;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int limitW = Math.Min(maxW, (int)(maxH * ratio.Value));
            int deltaW = Math.Abs(dVidX) > Math.Abs((int)(dVidY * ratio.Value)) ? dVidX : (int)(dVidY * ratio.Value);
            int newW = Math.Max(minSize, Math.Min(limitW, _viewModel.CropWidth + deltaW));
            int newH = (int)(newW / ratio.Value);

            _viewModel.CropX = left & ~1;
            _viewModel.CropY = top & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int newW = Math.Max(minSize, Math.Min(maxW, _viewModel.CropWidth + dVidX));
            int newH = Math.Max(minSize, Math.Min(maxH, _viewModel.CropHeight + dVidY));

            _viewModel.CropX = left & ~1;
            _viewModel.CropY = top & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_NW(double dx, double dy)
    {
        // Anchor: Bottom-Right (right, bottom) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidX = (int)(dx / scale);
        int dVidY = (int)(dy / scale);
        const int minSize = 64;

        int right = _viewModel.CropX + _viewModel.CropWidth;
        int bottom = _viewModel.CropY + _viewModel.CropHeight;
        int maxW = right;
        int maxH = bottom;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int limitW = Math.Min(maxW, (int)(maxH * ratio.Value));
            int deltaW = Math.Abs(dVidX) > Math.Abs((int)(dVidY * ratio.Value)) ? -dVidX : -(int)(dVidY * ratio.Value);
            int newW = Math.Max(minSize, Math.Min(limitW, _viewModel.CropWidth + deltaW));
            int newH = (int)(newW / ratio.Value);

            _viewModel.CropX = (right - newW) & ~1;
            _viewModel.CropY = (bottom - newH) & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int newX = Math.Max(0, Math.Min(right - minSize, _viewModel.CropX + dVidX));
            int newY = Math.Max(0, Math.Min(bottom - minSize, _viewModel.CropY + dVidY));
            int newW = right - newX;
            int newH = bottom - newY;

            _viewModel.CropX = newX & ~1;
            _viewModel.CropY = newY & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_NE(double dx, double dy)
    {
        // Anchor: Bottom-Left (left, bottom) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidX = (int)(dx / scale);
        int dVidY = (int)(dy / scale);
        const int minSize = 64;

        int left = _viewModel.CropX;
        int bottom = _viewModel.CropY + _viewModel.CropHeight;
        int maxW = (int)sw - left;
        int maxH = bottom;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int limitW = Math.Min(maxW, (int)(maxH * ratio.Value));
            int deltaW = Math.Abs(dVidX) > Math.Abs((int)(dVidY * ratio.Value)) ? dVidX : -(int)(dVidY * ratio.Value);
            int newW = Math.Max(minSize, Math.Min(limitW, _viewModel.CropWidth + deltaW));
            int newH = (int)(newW / ratio.Value);

            _viewModel.CropX = left & ~1;
            _viewModel.CropY = (bottom - newH) & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int newW = Math.Max(minSize, Math.Min(maxW, _viewModel.CropWidth + dVidX));
            int newY = Math.Max(0, Math.Min(bottom - minSize, _viewModel.CropY + dVidY));
            int newH = bottom - newY;

            _viewModel.CropX = left & ~1;
            _viewModel.CropY = newY & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_SW(double dx, double dy)
    {
        // Anchor: Top-Right (right, top) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        int dVidX = (int)(dx / scale);
        int dVidY = (int)(dy / scale);
        const int minSize = 64;

        int right = _viewModel.CropX + _viewModel.CropWidth;
        int top = _viewModel.CropY;
        int maxW = right;
        int maxH = (int)sh - top;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            int limitW = Math.Min(maxW, (int)(maxH * ratio.Value));
            int deltaW = Math.Abs(dVidX) > Math.Abs((int)(dVidY * ratio.Value)) ? -dVidX : (int)(dVidY * ratio.Value);
            int newW = Math.Max(minSize, Math.Min(limitW, _viewModel.CropWidth + deltaW));
            int newH = (int)(newW / ratio.Value);

            _viewModel.CropX = (right - newW) & ~1;
            _viewModel.CropY = top & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }
        else
        {
            int newX = Math.Max(0, Math.Min(right - minSize, _viewModel.CropX + dVidX));
            int newW = right - newX;
            int newH = Math.Max(minSize, Math.Min(maxH, _viewModel.CropHeight + dVidY));

            _viewModel.CropX = newX & ~1;
            _viewModel.CropY = top & ~1;
            _viewModel.CropWidth = newW & ~1;
            _viewModel.CropHeight = newH & ~1;
        }

        UpdateCropVisuals();
    }

    private void HandleNW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_NW(e.HorizontalChange, e.VerticalChange);
    private void HandleNE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_NE(e.HorizontalChange, e.VerticalChange);
    private void HandleSW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_SW(e.HorizontalChange, e.VerticalChange);
    private void HandleSE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_SE(e.HorizontalChange, e.VerticalChange);
    private void HandleN_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_N(e.VerticalChange);
    private void HandleS_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_S(e.VerticalChange);
    private void HandleW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_W(e.HorizontalChange);
    private void HandleE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ResizeHandle_E(e.HorizontalChange);

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
