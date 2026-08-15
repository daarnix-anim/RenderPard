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

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Loaded += TrimWindow_Loaded;
        SizeChanged += (s, e) => UpdateTimelineVisuals();
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
    }

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
