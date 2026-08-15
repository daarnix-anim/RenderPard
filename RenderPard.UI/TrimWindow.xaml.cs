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
    private bool _isDraggingCropBox;
    private Point _lastCropMousePosition;

    public TranscodeTask? CreatedTask => _viewModel.CreatedTasks.FirstOrDefault();
    public System.Collections.Generic.List<TranscodeTask> CreatedTasks => _viewModel.CreatedTasks;
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
        _viewModel.Segments.CollectionChanged += (s, e) => UpdateTimelineVisuals();

        Loaded += TrimWindow_Loaded;
        SizeChanged += (s, e) =>
        {
            UpdateTimelineVisuals();
            UpdateCropVisuals();
        };

        TimelineTrackGrid.SizeChanged += (s, e) => UpdateTimelineVisuals();
        MiniNavigatorGrid.SizeChanged += (s, e) => UpdateTimelineVisuals();
    }

    private async void TrimWindow_Loaded(object sender, RoutedEventArgs e)
    {
        string ext = Path.GetExtension(_viewModel.SourceFilePath).ToLower();
        bool isAudio = ext is ".mp3" or ".wav" or ".ogg" or ".opus" or ".flac" or ".aac" or ".m4a" or ".wma" or ".caf" or ".aiff";
        if (isAudio)
        {
            AudioVisualIndicator.Visibility = Visibility.Visible;
        }

        try
        {
            // Fallback metadata probe via FFprobe for instant duration/resolution
            var probeTask = new TranscodeTask { SourceFilePath = _viewModel.SourceFilePath };
            var ffmpeg = new RenderPard.Core.FFmpegWrapper();
            await ffmpeg.ProbeTaskAsync(probeTask);
            if (probeTask.DurationSeconds > 0 && _viewModel.TotalDurationSeconds <= 0)
            {
                _viewModel.TotalDurationSeconds = probeTask.DurationSeconds;
                if (probeTask.VideoWidth > 0 && probeTask.VideoHeight > 0)
                {
                    _viewModel.SetSourceResolution(probeTask.VideoWidth, probeTask.VideoHeight);
                }
                UpdateTimelineVisuals();
            }

            PlayerMediaElement.Source = new Uri(_viewModel.SourceFilePath, UriKind.Absolute);
            PlayerMediaElement.Play();

            // Give DirectX surface 60ms to initialize and present the first video frame, then pause
            await Task.Delay(60);
            if (!_viewModel.IsPlaying)
            {
                PlayerMediaElement.Pause();
                PlayerMediaElement.Position = TimeSpan.Zero;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть медиафайл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PlayerMediaElement_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (PlayerMediaElement.NaturalDuration.HasTimeSpan && PlayerMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
        {
            _viewModel.TotalDurationSeconds = PlayerMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            UpdateTimelineVisuals();
        }

        if (PlayerMediaElement.NaturalVideoWidth > 0 && PlayerMediaElement.NaturalVideoHeight > 0)
        {
            _viewModel.SetSourceResolution(PlayerMediaElement.NaturalVideoWidth, PlayerMediaElement.NaturalVideoHeight);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCropVisuals));
        }

        if (!PlayerMediaElement.HasAudio)
        {
            PlayerMediaElement.Volume = 0;
            PlayerMediaElement.IsMuted = true;
        }
    }

    private void PlayerMediaElement_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"MediaElement failed: {e.ErrorException?.Message}");
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

    private void CropCenterDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCropBox = true;
        _lastCropMousePosition = e.GetPosition(CropCanvas);
        CropCenterDragArea.CaptureMouse();
        e.Handled = true;
    }

    private void CropCenterDragArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingCropBox)
        {
            _isDraggingCropBox = false;
            CropCenterDragArea.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void CropCenterDragArea_MouseMove(object sender, MouseEventArgs e)
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

    private double _dragStartCropX;
    private double _dragStartCropY;
    private double _dragStartCropW;
    private double _dragStartCropH;
    private double _accumDragX;
    private double _accumDragY;

    private void CropHandle_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragStartCropX = _viewModel.CropX;
        _dragStartCropY = _viewModel.CropY;
        _dragStartCropW = _viewModel.CropWidth;
        _dragStartCropH = _viewModel.CropHeight;
        _accumDragX = 0;
        _accumDragY = 0;
    }

    private void ResizeHandle_SE(double dx, double dy)
    {
        // Anchor: Top-Left (anchorX, anchorY) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidX = dx / scale;
        double dVidY = dy / scale;
        const double minSize = 64;

        double anchorX = _dragStartCropX;
        double anchorY = _dragStartCropY;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double r = ratio.Value;
            double maxW = sw - anchorX;
            double maxH = sh - anchorY;
            double limitW = Math.Min(maxW, maxH * r);

            // Continuous 2D vector projection onto diagonal to eliminate slow-drag jitter
            double projected = (dVidX * r + dVidY) / (r * r + 1.0);
            double deltaW = projected * r;
            double targetW = _dragStartCropW + deltaW;
            double newW = Math.Max(minSize, Math.Min(limitW, targetW));
            double newH = newW / r;

            _viewModel.CropX = (int)Math.Round(anchorX) & ~1;
            _viewModel.CropY = (int)Math.Round(anchorY) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetRight = (_dragStartCropX + _dragStartCropW) + dVidX;
            double targetBottom = (_dragStartCropY + _dragStartCropH) + dVidY;

            double newRight = Math.Max(anchorX + minSize, Math.Min(sw, targetRight));
            double newBottom = Math.Max(anchorY + minSize, Math.Min(sh, targetBottom));

            _viewModel.CropX = (int)Math.Round(anchorX) & ~1;
            _viewModel.CropY = (int)Math.Round(anchorY) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newRight - anchorX) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newBottom - anchorY) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_NW(double dx, double dy)
    {
        // Anchor: Bottom-Right (anchorRight, anchorBottom) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidX = dx / scale;
        double dVidY = dy / scale;
        const double minSize = 64;

        double anchorRight = _dragStartCropX + _dragStartCropW;
        double anchorBottom = _dragStartCropY + _dragStartCropH;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double r = ratio.Value;
            double maxW = anchorRight;
            double maxH = anchorBottom;
            double limitW = Math.Min(maxW, maxH * r);

            // Continuous 2D vector projection (moving left & up expands)
            double projected = (-dVidX * r - dVidY) / (r * r + 1.0);
            double deltaW = projected * r;
            double targetW = _dragStartCropW + deltaW;
            double newW = Math.Max(minSize, Math.Min(limitW, targetW));
            double newH = newW / r;

            double newLeft = anchorRight - newW;
            double newTop = anchorBottom - newH;

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetLeft = _dragStartCropX + dVidX;
            double targetTop = _dragStartCropY + dVidY;

            double newLeft = Math.Max(0, Math.Min(anchorRight - minSize, targetLeft));
            double newTop = Math.Max(0, Math.Min(anchorBottom - minSize, targetTop));

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(anchorRight - newLeft) & ~1;
            _viewModel.CropHeight = (int)Math.Round(anchorBottom - newTop) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_NE(double dx, double dy)
    {
        // Anchor: Bottom-Left (anchorLeft, anchorBottom) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidX = dx / scale;
        double dVidY = dy / scale;
        const double minSize = 64;

        double anchorLeft = _dragStartCropX;
        double anchorBottom = _dragStartCropY + _dragStartCropH;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double r = ratio.Value;
            double maxW = sw - anchorLeft;
            double maxH = anchorBottom;
            double limitW = Math.Min(maxW, maxH * r);

            // Continuous 2D vector projection (moving right & up expands)
            double projected = (dVidX * r - dVidY) / (r * r + 1.0);
            double deltaW = projected * r;
            double targetW = _dragStartCropW + deltaW;
            double newW = Math.Max(minSize, Math.Min(limitW, targetW));
            double newH = newW / r;

            double newTop = anchorBottom - newH;

            _viewModel.CropX = (int)Math.Round(anchorLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetRight = (_dragStartCropX + _dragStartCropW) + dVidX;
            double targetTop = _dragStartCropY + dVidY;

            double newRight = Math.Max(anchorLeft + minSize, Math.Min(sw, targetRight));
            double newTop = Math.Max(0, Math.Min(anchorBottom - minSize, targetTop));

            _viewModel.CropX = (int)Math.Round(anchorLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newRight - anchorLeft) & ~1;
            _viewModel.CropHeight = (int)Math.Round(anchorBottom - newTop) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_SW(double dx, double dy)
    {
        // Anchor: Top-Right (anchorRight, anchorTop) is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidX = dx / scale;
        double dVidY = dy / scale;
        const double minSize = 64;

        double anchorRight = _dragStartCropX + _dragStartCropW;
        double anchorTop = _dragStartCropY;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double r = ratio.Value;
            double maxW = anchorRight;
            double maxH = sh - anchorTop;
            double limitW = Math.Min(maxW, maxH * r);

            // Continuous 2D vector projection (moving left & down expands)
            double projected = (-dVidX * r + dVidY) / (r * r + 1.0);
            double deltaW = projected * r;
            double targetW = _dragStartCropW + deltaW;
            double newW = Math.Max(minSize, Math.Min(limitW, targetW));
            double newH = newW / r;

            double newLeft = anchorRight - newW;

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(anchorTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetLeft = _dragStartCropX + dVidX;
            double targetBottom = (_dragStartCropY + _dragStartCropH) + dVidY;

            double newLeft = Math.Max(0, Math.Min(anchorRight - minSize, targetLeft));
            double newBottom = Math.Max(anchorTop + minSize, Math.Min(sh, targetBottom));

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(anchorTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(anchorRight - newLeft) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newBottom - anchorTop) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_N(double dy)
    {
        // Anchor: Bottom is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidY = dy / scale;
        const double minSize = 64;

        double anchorBottom = _dragStartCropY + _dragStartCropH;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double anchorCenterX = _dragStartCropX + _dragStartCropW / 2.0;
            double maxH = Math.Min(anchorBottom, sw / ratio.Value);

            double targetH = _dragStartCropH - dVidY;
            double newH = Math.Max(minSize, Math.Min(maxH, targetH));
            double newW = newH * ratio.Value;

            double newTop = anchorBottom - newH;
            double newLeft = Math.Max(0, Math.Min(sw - newW, anchorCenterX - newW / 2.0));

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetTop = _dragStartCropY + dVidY;
            double newTop = Math.Max(0, Math.Min(anchorBottom - minSize, targetTop));
            double newHeight = anchorBottom - newTop;

            _viewModel.CropX = (int)Math.Round(_dragStartCropX) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(_dragStartCropW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newHeight) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_S(double dy)
    {
        // Anchor: Top is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidY = dy / scale;
        const double minSize = 64;

        double anchorTop = _dragStartCropY;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double anchorCenterX = _dragStartCropX + _dragStartCropW / 2.0;
            double maxH = Math.Min(sh - anchorTop, sw / ratio.Value);

            double targetH = _dragStartCropH + dVidY;
            double newH = Math.Max(minSize, Math.Min(maxH, targetH));
            double newW = newH * ratio.Value;

            double newLeft = Math.Max(0, Math.Min(sw - newW, anchorCenterX - newW / 2.0));

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(anchorTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetBottom = (_dragStartCropY + _dragStartCropH) + dVidY;
            double newBottom = Math.Max(anchorTop + minSize, Math.Min(sh, targetBottom));
            double newHeight = newBottom - anchorTop;

            _viewModel.CropX = (int)Math.Round(_dragStartCropX) & ~1;
            _viewModel.CropY = (int)Math.Round(anchorTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(_dragStartCropW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newHeight) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_W(double dx)
    {
        // Anchor: Right is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidX = dx / scale;
        const double minSize = 64;

        double anchorRight = _dragStartCropX + _dragStartCropW;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double anchorCenterY = _dragStartCropY + _dragStartCropH / 2.0;
            double maxW = Math.Min(anchorRight, sh * ratio.Value);

            double targetW = _dragStartCropW - dVidX;
            double newW = Math.Max(minSize, Math.Min(maxW, targetW));
            double newH = newW / ratio.Value;

            double newLeft = anchorRight - newW;
            double newTop = Math.Max(0, Math.Min(sh - newH, anchorCenterY - newH / 2.0));

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetLeft = _dragStartCropX + dVidX;
            double newLeft = Math.Max(0, Math.Min(anchorRight - minSize, targetLeft));
            double newWidth = anchorRight - newLeft;

            _viewModel.CropX = (int)Math.Round(newLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(_dragStartCropY) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newWidth) & ~1;
            _viewModel.CropHeight = (int)Math.Round(_dragStartCropH) & ~1;
        }

        UpdateCropVisuals();
    }

    private void ResizeHandle_E(double dx)
    {
        // Anchor: Left is strictly fixed
        var vRect = GetVideoDisplayRect();
        double sw = _viewModel.SourceVideoWidth > 0 ? _viewModel.SourceVideoWidth : 1920;
        double sh = _viewModel.SourceVideoHeight > 0 ? _viewModel.SourceVideoHeight : 1080;
        double scale = vRect.Width / sw;
        if (scale <= 0) return;

        double dVidX = dx / scale;
        const double minSize = 64;

        double anchorLeft = _dragStartCropX;

        double? ratio = GetLockedRatio();

        if (ratio.HasValue)
        {
            double anchorCenterY = _dragStartCropY + _dragStartCropH / 2.0;
            double maxW = Math.Min(sw - anchorLeft, sh * ratio.Value);

            double targetW = _dragStartCropW + dVidX;
            double newW = Math.Max(minSize, Math.Min(maxW, targetW));
            double newH = newW / ratio.Value;

            double newTop = Math.Max(0, Math.Min(sh - newH, anchorCenterY - newH / 2.0));

            _viewModel.CropX = (int)Math.Round(anchorLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(newTop) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newW) & ~1;
            _viewModel.CropHeight = (int)Math.Round(newH) & ~1;
        }
        else
        {
            double targetRight = (_dragStartCropX + _dragStartCropW) + dVidX;
            double newRight = Math.Max(anchorLeft + minSize, Math.Min(sw, targetRight));
            double newWidth = newRight - anchorLeft;

            _viewModel.CropX = (int)Math.Round(anchorLeft) & ~1;
            _viewModel.CropY = (int)Math.Round(_dragStartCropY) & ~1;
            _viewModel.CropWidth = (int)Math.Round(newWidth) & ~1;
            _viewModel.CropHeight = (int)Math.Round(_dragStartCropH) & ~1;
        }

        UpdateCropVisuals();
    }

    private void HandleNW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragX += e.HorizontalChange;
        _accumDragY += e.VerticalChange;
        ResizeHandle_NW(_accumDragX, _accumDragY);
    }

    private void HandleNE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragX += e.HorizontalChange;
        _accumDragY += e.VerticalChange;
        ResizeHandle_NE(_accumDragX, _accumDragY);
    }

    private void HandleSW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragX += e.HorizontalChange;
        _accumDragY += e.VerticalChange;
        ResizeHandle_SW(_accumDragX, _accumDragY);
    }

    private void HandleSE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragX += e.HorizontalChange;
        _accumDragY += e.VerticalChange;
        ResizeHandle_SE(_accumDragX, _accumDragY);
    }

    private void HandleN_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragY += e.VerticalChange;
        ResizeHandle_N(_accumDragY);
    }

    private void HandleS_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragY += e.VerticalChange;
        ResizeHandle_S(_accumDragY);
    }

    private void HandleW_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragX += e.HorizontalChange;
        ResizeHandle_W(_accumDragX);
    }

    private void HandleE_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        _accumDragX += e.HorizontalChange;
        ResizeHandle_E(_accumDragX);
    }

    private void PlayerMediaElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPlaying = false;
        _playbackTimer.Stop();
        _viewModel.SeekTo(_viewModel.InPointSeconds ?? 0);
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isScrubbingTimeline && PlayerMediaElement.NaturalDuration.HasTimeSpan)
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
        if (!_viewModel.IsPlaying)
        {
            PlayerMediaElement.Play();
            PlayerMediaElement.Pause();
        }
    }

    private void OnRequestExport(TranscodeTask task, bool startImmediately)
    {
        StartImmediately = startImmediately;
        DialogResult = true;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrimViewModel.InPointSeconds) or 
            nameof(TrimViewModel.OutPointSeconds) or 
            nameof(TrimViewModel.TotalDurationSeconds) or
            nameof(TrimViewModel.ViewportStart) or
            nameof(TrimViewModel.ViewportEnd) or
            nameof(TrimViewModel.ZoomLevel) or
            nameof(TrimViewModel.CurrentTimeSeconds))
        {
            UpdateTimelineVisuals();
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void UpdateTimelineVisuals()
    {
        double trackWidth = TimelineTrackGrid.ActualWidth > 0 ? TimelineTrackGrid.ActualWidth : TimelineSlider.ActualWidth;
        double totDur = _viewModel.TotalDurationSeconds;
        if (totDur <= 0) return;

        double vStart = _viewModel.ViewportStart;
        double vEnd = _viewModel.ViewportEnd > 0 ? _viewModel.ViewportEnd : totDur;
        double vDur = Math.Max(0.001, vEnd - vStart);

        // 1. Zoomed Main Timeline markers
        if (trackWidth > 0)
        {
            double inSec = _viewModel.InPointSeconds ?? 0;
            double outSec = _viewModel.OutPointSeconds ?? totDur;

            double inX = ((inSec - vStart) / vDur) * trackWidth;
            double outX = ((outSec - vStart) / vDur) * trackWidth;

            Canvas.SetLeft(InBracketThumb, Math.Max(-14, Math.Min(trackWidth, inX - 14)));
            Canvas.SetLeft(OutBracketThumb, Math.Max(0, Math.Min(trackWidth, outX)));

            double hLeft = Math.Max(0, inX);
            double hRight = Math.Min(trackWidth, outX);
            Canvas.SetLeft(TrimHighlightBar, hLeft);
            TrimHighlightBar.Width = Math.Max(0, hRight - hLeft);
        }

        // 2. Mini Navigator Overview Bar
        double navWidth = MiniNavigatorGrid.ActualWidth;
        if (navWidth > 0)
        {
            // Mini In/Out range
            double miniInFrac = (_viewModel.InPointSeconds ?? 0) / totDur;
            double miniOutFrac = (_viewModel.OutPointSeconds ?? totDur) / totDur;
            Canvas.SetLeft(MiniInHighlight, miniInFrac * navWidth);
            MiniInHighlight.Width = Math.Max(0, (miniOutFrac - miniInFrac) * navWidth);

            // Mini Playhead
            double miniPlayFrac = _viewModel.CurrentTimeSeconds / totDur;
            Canvas.SetLeft(MiniPlayheadMarker, Math.Max(0, Math.Min(navWidth - 3, miniPlayFrac * navWidth)));

            // Mini Viewport Window with Left/Right resize handles
            double vpStartFrac = vStart / totDur;
            double vpEndFrac = vEnd / totDur;
            double boxX = vpStartFrac * navWidth;
            double boxW = Math.Max(22, (vpEndFrac - vpStartFrac) * navWidth);
            Canvas.SetLeft(MiniViewportGrid, boxX);
            MiniViewportGrid.Width = Math.Min(navWidth - boxX, boxW);
        }

        // 3. Multi-Segment Visual Markers on both Mini and Main Timelines
        MiniSegmentsCanvas.Children.Clear();
        MainSegmentsCanvas.Children.Clear();

        if (_viewModel.Segments.Count > 0)
        {
            foreach (var seg in _viewModel.Segments)
            {
                // Mini Navigator Canvas
                if (navWidth > 0)
                {
                    double sInFrac = seg.StartSeconds / totDur;
                    double sOutFrac = seg.EndSeconds / totDur;
                    double segX = sInFrac * navWidth;
                    double segW = Math.Max(3, (sOutFrac - sInFrac) * navWidth);

                    var miniSegBorder = new Border
                    {
                        Width = segW,
                        Height = 8,
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 0, 200, 255)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 255)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(2)
                    };
                    Canvas.SetLeft(miniSegBorder, segX);
                    MiniSegmentsCanvas.Children.Add(miniSegBorder);
                }

                // Main Zoomed Timeline Canvas
                if (trackWidth > 0 && seg.EndSeconds >= vStart && seg.StartSeconds <= vEnd)
                {
                    double mInX = ((seg.StartSeconds - vStart) / vDur) * trackWidth;
                    double mOutX = ((seg.EndSeconds - vStart) / vDur) * trackWidth;
                    double mLeft = Math.Max(0, mInX);
                    double mRight = Math.Min(trackWidth, mOutX);

                    if (mRight > mLeft)
                    {
                        var mainSegBorder = new Border
                        {
                            Width = mRight - mLeft,
                            Height = 10,
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 0, 200, 255)),
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 200, 255)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(2)
                        };
                        Canvas.SetLeft(mainSegBorder, mLeft);
                        MainSegmentsCanvas.Children.Add(mainSegBorder);
                    }
                }
            }
        }
    }

    // Direct Interactive In/Out Drag Brackets
    private double _dragStartInSec;
    private double _dragStartOutSec;
    private double _accumBracketDragX;

    private void InBracketThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragStartInSec = _viewModel.InPointSeconds ?? 0;
        _accumBracketDragX = 0;
    }

    private void InBracketThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        double trackWidth = TimelineTrackGrid.ActualWidth > 0 ? TimelineTrackGrid.ActualWidth : TimelineSlider.ActualWidth;
        double totDur = _viewModel.TotalDurationSeconds;
        if (trackWidth <= 0 || totDur <= 0) return;

        double vStart = _viewModel.ViewportStart;
        double vEnd = _viewModel.ViewportEnd > 0 ? _viewModel.ViewportEnd : totDur;
        double vDur = Math.Max(0.001, vEnd - vStart);

        _accumBracketDragX += e.HorizontalChange;
        double deltaSec = (_accumBracketDragX / trackWidth) * vDur;

        double maxIn = (_viewModel.OutPointSeconds ?? totDur) - 0.05;
        double newIn = Math.Max(0, Math.Min(maxIn, _dragStartInSec + deltaSec));

        _viewModel.InPointSeconds = newIn;
        _viewModel.SeekTo(newIn);
        UpdateTimelineVisuals();
    }

    private void OutBracketThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragStartOutSec = _viewModel.OutPointSeconds ?? _viewModel.TotalDurationSeconds;
        _accumBracketDragX = 0;
    }

    private void OutBracketThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        double trackWidth = TimelineTrackGrid.ActualWidth > 0 ? TimelineTrackGrid.ActualWidth : TimelineSlider.ActualWidth;
        double totDur = _viewModel.TotalDurationSeconds;
        if (trackWidth <= 0 || totDur <= 0) return;

        double vStart = _viewModel.ViewportStart;
        double vEnd = _viewModel.ViewportEnd > 0 ? _viewModel.ViewportEnd : totDur;
        double vDur = Math.Max(0.001, vEnd - vStart);

        _accumBracketDragX += e.HorizontalChange;
        double deltaSec = (_accumBracketDragX / trackWidth) * vDur;

        double minOut = (_viewModel.InPointSeconds ?? 0) + 0.05;
        double newOut = Math.Max(minOut, Math.Min(totDur, _dragStartOutSec + deltaSec));

        _viewModel.OutPointSeconds = newOut;
        _viewModel.SeekTo(newOut);
        UpdateTimelineVisuals();
    }

    // Direct Click & Drag to Seek anywhere across the timeline track
    private bool _isScrubbingTimeline = false;

    private void TimelineTrackGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<System.Windows.Controls.Primitives.Thumb>(e.OriginalSource as DependencyObject) != null)
        {
            // Allow Thumb (e.g. InBracketThumb / OutBracketThumb) to handle dragging without moving playhead
            return;
        }

        _isScrubbingTimeline = true;
        TimelineTrackGrid.CaptureMouse();
        SeekTimelineFromMouse(e.GetPosition(TimelineTrackGrid));
        e.Handled = true;
    }

    private void TimelineTrackGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isScrubbingTimeline)
        {
            SeekTimelineFromMouse(e.GetPosition(TimelineTrackGrid));
            e.Handled = true;
        }
    }

    private void TimelineTrackGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isScrubbingTimeline)
        {
            _isScrubbingTimeline = false;
            TimelineTrackGrid.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void SeekTimelineFromMouse(Point pos)
    {
        double trackW = TimelineTrackGrid.ActualWidth;
        double totDur = _viewModel.TotalDurationSeconds;
        if (trackW <= 0 || totDur <= 0) return;

        double vStart = _viewModel.ViewportStart;
        double vEnd = _viewModel.ViewportEnd > 0 ? _viewModel.ViewportEnd : totDur;
        double vDur = Math.Max(0.001, vEnd - vStart);

        double ratio = Math.Max(0, Math.Min(1.0, pos.X / trackW));
        double targetSec = vStart + ratio * vDur;

        _viewModel.CurrentTimeSeconds = targetSec;
        _viewModel.SeekTo(targetSec);
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isScrubbingTimeline)
        {
            _viewModel.SeekTo(e.NewValue);
        }
    }

    private void TimelineTrack_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Zoom centered at mouse position
            Point pos = e.GetPosition(TimelineTrackGrid);
            double frac = Math.Max(0, Math.Min(1.0, pos.X / Math.Max(1.0, TimelineTrackGrid.ActualWidth)));
            double mouseTime = _viewModel.ViewportStart + frac * (_viewModel.ViewportEnd - _viewModel.ViewportStart);

            if (e.Delta > 0)
                _viewModel.SetZoom(_viewModel.ZoomLevel * 1.25, mouseTime);
            else if (e.Delta < 0)
                _viewModel.SetZoom(_viewModel.ZoomLevel / 1.25, mouseTime);

            UpdateTimelineVisuals();
            e.Handled = true;
        }
        else if (_viewModel.IsZoomed)
        {
            // Pan with mouse wheel
            double windowDur = _viewModel.ViewportEnd - _viewModel.ViewportStart;
            double panStep = (windowDur * 0.1) * (e.Delta > 0 ? -1 : 1);
            _viewModel.PanViewport(panStep);
            UpdateTimelineVisuals();
            e.Handled = true;
        }
    }

    // Premiere / After Effects style Zoom Navigator Bar handlers
    private void MiniZoomHandleLeft_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        double navWidth = MiniNavigatorGrid.ActualWidth;
        double totDur = _viewModel.TotalDurationSeconds;
        if (navWidth <= 0 || totDur <= 0) return;

        double deltaSec = (e.HorizontalChange / navWidth) * totDur;
        double newStart = _viewModel.ViewportStart + deltaSec;
        _viewModel.SetViewportRange(newStart, _viewModel.ViewportEnd);
        UpdateTimelineVisuals();
    }

    private void MiniZoomHandleRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        double navWidth = MiniNavigatorGrid.ActualWidth;
        double totDur = _viewModel.TotalDurationSeconds;
        if (navWidth <= 0 || totDur <= 0) return;

        double deltaSec = (e.HorizontalChange / navWidth) * totDur;
        double newEnd = _viewModel.ViewportEnd + deltaSec;
        _viewModel.SetViewportRange(_viewModel.ViewportStart, newEnd);
        UpdateTimelineVisuals();
    }

    private bool _isPanningMiniZoomCenter = false;
    private Point _lastMiniZoomMousePos;

    private void MiniZoomCenterBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanningMiniZoomCenter = true;
        _lastMiniZoomMousePos = e.GetPosition(MiniNavigatorGrid);
        MiniZoomCenterBar.CaptureMouse();
        e.Handled = true;
    }

    private void MiniZoomCenterBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningMiniZoomCenter)
        {
            _isPanningMiniZoomCenter = false;
            MiniZoomCenterBar.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void MiniZoomCenterBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanningMiniZoomCenter || _viewModel.TotalDurationSeconds <= 0 || MiniNavigatorGrid.ActualWidth <= 0) return;
        Point currentPos = e.GetPosition(MiniNavigatorGrid);
        double deltaX = currentPos.X - _lastMiniZoomMousePos.X;
        _lastMiniZoomMousePos = currentPos;

        double deltaSec = (deltaX / MiniNavigatorGrid.ActualWidth) * _viewModel.TotalDurationSeconds;
        _viewModel.PanViewport(deltaSec);
        UpdateTimelineVisuals();
        e.Handled = true;
    }

    private void MiniNavigator_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && (dep is System.Windows.Controls.Primitives.Thumb || FindVisualParent<System.Windows.Controls.Primitives.Thumb>(dep) != null || dep == MiniZoomCenterBar))
        {
            return;
        }

        if (_viewModel.TotalDurationSeconds <= 0 || MiniNavigatorGrid.ActualWidth <= 0) return;
        Point pos = e.GetPosition(MiniNavigatorGrid);
        double frac = Math.Max(0, Math.Min(1.0, pos.X / MiniNavigatorGrid.ActualWidth));
        double centerSec = frac * _viewModel.TotalDurationSeconds;
        _viewModel.UpdateViewportFromZoom(centerSec);
        _viewModel.CurrentTimeSeconds = centerSec;
        _viewModel.SeekTo(centerSec);
        UpdateTimelineVisuals();
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
        else if (e.Key == Key.M)
        {
            _viewModel.AddCurrentSegmentCommand.Execute(null);
            UpdateTimelineVisuals();
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
