using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenderPard.Core;
using RenderPard.Core.Models;

namespace RenderPard.UI.ViewModels;

public enum CropAspectRatioMode
{
    None,
    Vertical9x16,
    Horizontal16x9,
    Square1x1,
    Custom
}

public partial class TrimViewModel : ObservableObject
{
    [ObservableProperty]
    private string _sourceFilePath = string.Empty;

    public string FileName => Path.GetFileName(SourceFilePath);

    [ObservableProperty]
    private double _totalDurationSeconds;

    [ObservableProperty]
    private double _currentTimeSeconds;

    [ObservableProperty]
    private double? _inPointSeconds;

    [ObservableProperty]
    private double? _outPointSeconds;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private bool _isLosslessCopy;

    [ObservableProperty]
    private Preset? _selectedPreset;

    // Timeline Zoom & Viewport properties (Premiere-style scaling for long videos)
    [ObservableProperty]
    private double _zoomLevel = 1.0; // 1.0 = full video, 20.0 = 20x zoom

    [ObservableProperty]
    private double _viewportStart = 0;

    [ObservableProperty]
    private double _viewportEnd = 0;

    public bool IsZoomed => ZoomLevel > 1.001;

    public string ZoomText => $"{ZoomLevel:0.#}x";

    // Crop / Framing properties
    [ObservableProperty]
    private CropAspectRatioMode _cropMode = CropAspectRatioMode.None;

    public bool IsCropActive => CropMode != CropAspectRatioMode.None;

    [ObservableProperty]
    private int _sourceVideoWidth;

    [ObservableProperty]
    private int _sourceVideoHeight;

    [ObservableProperty]
    private int _cropX;

    [ObservableProperty]
    private int _cropY;

    [ObservableProperty]
    private int _cropWidth;

    [ObservableProperty]
    private int _cropHeight;

    public string CropInfoText
    {
        get
        {
            if (!IsCropActive || CropWidth <= 0 || CropHeight <= 0) return "Без кропа";
            string modeName = CropMode switch
            {
                CropAspectRatioMode.Vertical9x16 => "9:16",
                CropAspectRatioMode.Horizontal16x9 => "16:9",
                CropAspectRatioMode.Square1x1 => "1:1",
                CropAspectRatioMode.Custom => "Custom",
                _ => ""
            };
            return $"{CropWidth} × {CropHeight} ({modeName})";
        }
    }

    public ObservableCollection<Preset> AvailablePresets { get; } = new();

    public event Action? RequestPlay;
    public event Action? RequestPause;
    public event Action<double>? RequestSeek;
    public event Action<TranscodeTask, bool>? RequestExport; // task, startImmediately
    public event Action? RequestClose;
    public event Action? RequestCropOverlayUpdate;

    public string CurrentTimeText => FormatTime(CurrentTimeSeconds);
    public string TotalDurationText => FormatTime(TotalDurationSeconds);

    public string InPointText
    {
        get => InPointSeconds.HasValue ? FormatTime(InPointSeconds.Value) : "00:00:00.000";
        set
        {
            if (TryParseTime(value, out double sec))
            {
                InPointSeconds = Math.Max(0, Math.Min(sec, OutPointSeconds ?? TotalDurationSeconds));
                OnPropertyChanged(nameof(InPointText));
                OnPropertyChanged(nameof(SelectedDurationText));
            }
        }
    }

    public string OutPointText
    {
        get => OutPointSeconds.HasValue ? FormatTime(OutPointSeconds.Value) : FormatTime(TotalDurationSeconds);
        set
        {
            if (TryParseTime(value, out double sec))
            {
                OutPointSeconds = Math.Min(TotalDurationSeconds, Math.Max(sec, InPointSeconds ?? 0));
                OnPropertyChanged(nameof(OutPointText));
                OnPropertyChanged(nameof(SelectedDurationText));
            }
        }
    }

    public string SelectedDurationText
    {
        get
        {
            double start = InPointSeconds ?? 0;
            double end = OutPointSeconds ?? TotalDurationSeconds;
            double dur = Math.Max(0, end - start);
            return FormatTime(dur);
        }
    }

    public double InPointFraction => TotalDurationSeconds > 0 ? (InPointSeconds ?? 0) / TotalDurationSeconds : 0;
    public double OutPointFraction => TotalDurationSeconds > 0 ? (OutPointSeconds ?? TotalDurationSeconds) / TotalDurationSeconds : 1;
    public double SelectedDurationFraction => Math.Max(0, OutPointFraction - InPointFraction);

    public TrimViewModel(string filePath, Preset? initialPreset = null)
    {
        SourceFilePath = filePath;
        
        // Load presets
        var presets = App.PresetManager.LoadPresets();
        foreach (var p in presets)
        {
            AvailablePresets.Add(p);
        }

        if (initialPreset != null)
        {
            SelectedPreset = AvailablePresets.FirstOrDefault(p => p.Name == initialPreset.Name) ?? AvailablePresets.FirstOrDefault();
        }
        else
        {
            SelectedPreset = AvailablePresets.FirstOrDefault();
        }
    }

    partial void OnCropModeChanged(CropAspectRatioMode value)
    {
        OnPropertyChanged(nameof(IsCropActive));
        OnPropertyChanged(nameof(CropInfoText));

        if (value != CropAspectRatioMode.None && IsLosslessCopy)
        {
            IsLosslessCopy = false; // Stream copy cannot crop resolution
        }

        ApplyCropAspectRatio(value);
        RequestCropOverlayUpdate?.Invoke();
    }

    partial void OnCropXChanged(int value) => OnPropertyChanged(nameof(CropInfoText));
    partial void OnCropYChanged(int value) => OnPropertyChanged(nameof(CropInfoText));
    partial void OnCropWidthChanged(int value) => OnPropertyChanged(nameof(CropInfoText));
    partial void OnCropHeightChanged(int value) => OnPropertyChanged(nameof(CropInfoText));

    [RelayCommand]
    public void SetCropMode(CropAspectRatioMode mode)
    {
        CropMode = mode;
    }

    [RelayCommand]
    public void ResetCrop()
    {
        CropMode = CropAspectRatioMode.None;
    }

    public void SetSourceResolution(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        SourceVideoWidth = width;
        SourceVideoHeight = height;
        if (CropMode != CropAspectRatioMode.None)
        {
            ApplyCropAspectRatio(CropMode);
        }
    }

    public void ApplyCropAspectRatio(CropAspectRatioMode mode)
    {
        int sw = SourceVideoWidth > 0 ? SourceVideoWidth : 1920;
        int sh = SourceVideoHeight > 0 ? SourceVideoHeight : 1080;

        switch (mode)
        {
            case CropAspectRatioMode.None:
                CropX = 0;
                CropY = 0;
                CropWidth = sw;
                CropHeight = sh;
                break;

            case CropAspectRatioMode.Vertical9x16:
                {
                    // 9:16 vertical crop centered
                    int targetW = (int)(sh * 9.0 / 16.0);
                    int targetH = sh;
                    if (targetW > sw)
                    {
                        targetW = sw;
                        targetH = (int)(sw * 16.0 / 9.0);
                    }
                    targetW &= ~1;
                    targetH &= ~1;
                    CropWidth = Math.Max(2, targetW);
                    CropHeight = Math.Max(2, targetH);
                    CropX = Math.Max(0, (sw - CropWidth) / 2) & ~1;
                    CropY = Math.Max(0, (sh - CropHeight) / 2) & ~1;
                }
                break;

            case CropAspectRatioMode.Horizontal16x9:
                {
                    // 16:9 horizontal crop centered
                    int targetW = sw;
                    int targetH = (int)(sw * 9.0 / 16.0);
                    if (targetH > sh)
                    {
                        targetH = sh;
                        targetW = (int)(sh * 16.0 / 9.0);
                    }
                    targetW &= ~1;
                    targetH &= ~1;
                    CropWidth = Math.Max(2, targetW);
                    CropHeight = Math.Max(2, targetH);
                    CropX = Math.Max(0, (sw - CropWidth) / 2) & ~1;
                    CropY = Math.Max(0, (sh - CropHeight) / 2) & ~1;
                }
                break;

            case CropAspectRatioMode.Square1x1:
                {
                    // 1:1 square crop centered
                    int size = Math.Min(sw, sh) & ~1;
                    CropWidth = Math.Max(2, size);
                    CropHeight = Math.Max(2, size);
                    CropX = Math.Max(0, (sw - size) / 2) & ~1;
                    CropY = Math.Max(0, (sh - size) / 2) & ~1;
                }
                break;

            case CropAspectRatioMode.Custom:
                if (CropWidth <= 0 || CropHeight <= 0 || CropWidth == sw && CropHeight == sh)
                {
                    // Default to 80% box in the center
                    int cw = (int)(sw * 0.8) & ~1;
                    int ch = (int)(sh * 0.8) & ~1;
                    CropWidth = Math.Max(2, cw);
                    CropHeight = Math.Max(2, ch);
                    CropX = Math.Max(0, (sw - CropWidth) / 2) & ~1;
                    CropY = Math.Max(0, (sh - CropHeight) / 2) & ~1;
                }
                break;
        }
    }

    partial void OnCurrentTimeSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentTimeText));

        // Auto-pan viewport if playhead moves outside current zoom window
        if (IsZoomed && TotalDurationSeconds > 0)
        {
            double windowDur = ViewportEnd - ViewportStart;
            if (value < ViewportStart)
            {
                ViewportStart = Math.Max(0, value - windowDur * 0.1);
                ViewportEnd = Math.Min(TotalDurationSeconds, ViewportStart + windowDur);
            }
            else if (value > ViewportEnd)
            {
                ViewportEnd = Math.Min(TotalDurationSeconds, value + windowDur * 0.1);
                ViewportStart = Math.Max(0, ViewportEnd - windowDur);
            }
        }
    }

    partial void OnZoomLevelChanged(double value)
    {
        OnPropertyChanged(nameof(IsZoomed));
        OnPropertyChanged(nameof(ZoomText));
        UpdateViewportFromZoom(CurrentTimeSeconds);
    }

    public void SetZoom(double level, double? centerTime = null)
    {
        level = Math.Max(1.0, Math.Min(30.0, level));
        ZoomLevel = level;
        UpdateViewportFromZoom(centerTime ?? CurrentTimeSeconds);
    }

    public void UpdateViewportFromZoom(double centerTime)
    {
        if (TotalDurationSeconds <= 0) return;

        if (ZoomLevel <= 1.001)
        {
            ViewportStart = 0;
            ViewportEnd = TotalDurationSeconds;
            return;
        }

        double windowDur = TotalDurationSeconds / ZoomLevel;
        double start = centerTime - windowDur / 2;
        if (start < 0) start = 0;
        if (start + windowDur > TotalDurationSeconds) start = Math.Max(0, TotalDurationSeconds - windowDur);

        ViewportStart = start;
        ViewportEnd = Math.Min(TotalDurationSeconds, start + windowDur);
    }

    public void SetViewportRange(double start, double end)
    {
        if (TotalDurationSeconds <= 0) return;
        const double minDur = 0.5;
        start = Math.Max(0, Math.Min(TotalDurationSeconds - minDur, start));
        end = Math.Min(TotalDurationSeconds, Math.Max(start + minDur, end));

        ViewportStart = start;
        ViewportEnd = end;
        double windowDur = end - start;
        ZoomLevel = Math.Max(1.0, Math.Min(50.0, TotalDurationSeconds / windowDur));
    }

    public void PanViewport(double deltaSeconds)
    {
        if (TotalDurationSeconds <= 0 || ZoomLevel <= 1.001) return;
        double windowDur = ViewportEnd - ViewportStart;
        double newStart = Math.Max(0, Math.Min(TotalDurationSeconds - windowDur, ViewportStart + deltaSeconds));
        ViewportStart = newStart;
        ViewportEnd = newStart + windowDur;
    }

    [RelayCommand]
    public void ZoomIn() => SetZoom(ZoomLevel * 1.5);

    [RelayCommand]
    public void ZoomOut() => SetZoom(ZoomLevel / 1.5);

    [RelayCommand]
    public void ResetZoom() => SetZoom(1.0);

    partial void OnInPointSecondsChanged(double? value)
    {
        OnPropertyChanged(nameof(InPointText));
        OnPropertyChanged(nameof(SelectedDurationText));
        OnPropertyChanged(nameof(InPointFraction));
        OnPropertyChanged(nameof(SelectedDurationFraction));
    }

    partial void OnOutPointSecondsChanged(double? value)
    {
        OnPropertyChanged(nameof(OutPointText));
        OnPropertyChanged(nameof(SelectedDurationText));
        OnPropertyChanged(nameof(OutPointFraction));
        OnPropertyChanged(nameof(SelectedDurationFraction));
    }

    partial void OnTotalDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(OutPointText));
        OnPropertyChanged(nameof(SelectedDurationText));
        OnPropertyChanged(nameof(InPointFraction));
        OnPropertyChanged(nameof(OutPointFraction));
        OnPropertyChanged(nameof(SelectedDurationFraction));

        if (ViewportEnd <= 0 || ZoomLevel <= 1.001)
        {
            ViewportStart = 0;
            ViewportEnd = value;
        }
    }

    [RelayCommand]
    public void SetInPoint()
    {
        double current = CurrentTimeSeconds;
        if (OutPointSeconds.HasValue && current >= OutPointSeconds.Value)
        {
            current = Math.Max(0, OutPointSeconds.Value - 0.1);
        }
        InPointSeconds = current;
    }

    [RelayCommand]
    public void SetOutPoint()
    {
        double current = CurrentTimeSeconds;
        if (InPointSeconds.HasValue && current <= InPointSeconds.Value)
        {
            current = Math.Min(TotalDurationSeconds, InPointSeconds.Value + 0.1);
        }
        OutPointSeconds = current;
    }

    [RelayCommand]
    public void ClearInPoint()
    {
        InPointSeconds = null;
    }

    [RelayCommand]
    public void ClearOutPoint()
    {
        OutPointSeconds = null;
    }

    [RelayCommand]
    public void ResetTrim()
    {
        InPointSeconds = null;
        OutPointSeconds = null;
    }

    [RelayCommand]
    public void JumpToInPoint()
    {
        double target = InPointSeconds ?? 0;
        SeekTo(target);
    }

    [RelayCommand]
    public void JumpToOutPoint()
    {
        double target = OutPointSeconds ?? TotalDurationSeconds;
        SeekTo(target);
    }

    [RelayCommand]
    public void TogglePlay()
    {
        if (IsPlaying)
        {
            RequestPause?.Invoke();
            IsPlaying = false;
        }
        else
        {
            RequestPlay?.Invoke();
            IsPlaying = true;
        }
    }

    [RelayCommand]
    public void StepFrameForward()
    {
        SeekRelative(1.0 / 30.0);
    }

    [RelayCommand]
    public void StepFrameBackward()
    {
        SeekRelative(-1.0 / 30.0);
    }

    [RelayCommand]
    public void StepSecondForward()
    {
        SeekRelative(1.0);
    }

    [RelayCommand]
    public void StepSecondBackward()
    {
        SeekRelative(-1.0);
    }

    public void SeekTo(double seconds)
    {
        double clamped = Math.Max(0, Math.Min(seconds, TotalDurationSeconds));
        CurrentTimeSeconds = clamped;
        RequestSeek?.Invoke(clamped);
    }

    public void SeekRelative(double delta)
    {
        SeekTo(CurrentTimeSeconds + delta);
    }

    [RelayCommand]
    public void AddToQueue()
    {
        var task = CreateTranscodeTask();
        RequestExport?.Invoke(task, false);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void RenderNow()
    {
        var task = CreateTranscodeTask();
        RequestExport?.Invoke(task, true);
        RequestClose?.Invoke();
    }

    private TranscodeTask CreateTranscodeTask()
    {
        var preset = SelectedPreset ?? AvailablePresets.FirstOrDefault() ?? new Preset { Name = "Default" };
        
        string dir = Path.GetDirectoryName(SourceFilePath) ?? "";
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(SourceFilePath);

        string ext;
        if (IsLosslessCopy)
        {
            ext = Path.GetExtension(SourceFilePath);
        }
        else if (preset.IsAudioPreset)
        {
            ext = preset.Container switch
            {
                ContainerFormat.Wav => ".wav",
                ContainerFormat.Ogg => ".ogg",
                ContainerFormat.Flac => ".flac",
                ContainerFormat.Aac => ".m4a",
                _ => ".mp3"
            };
        }
        else if (preset.IsImagePreset)
        {
            ext = preset.Container switch
            {
                ContainerFormat.Webp => ".webp",
                ContainerFormat.Png => ".png",
                _ => ".jpg"
            };
        }
        else
        {
            ext = preset.Container switch
            {
                ContainerFormat.WebM => ".webm",
                ContainerFormat.Gif => ".gif",
                ContainerFormat.MXF => ".mxf",
                _ => ".mp4"
            };
        }

        string targetFileName = $"{fileNameWithoutExt}_trim{ext}";
        string targetFilePath = Path.Combine(dir, targetFileName);

        int counter = 1;
        while (File.Exists(targetFilePath))
        {
            targetFileName = $"{fileNameWithoutExt}_trim_{counter}{ext}";
            targetFilePath = Path.Combine(dir, targetFileName);
            counter++;
        }

        var task = new TranscodeTask
        {
            SourceFilePath = SourceFilePath,
            TargetFilePath = targetFilePath,
            Preset = preset,
            DurationSeconds = TotalDurationSeconds,
            TrimStartSeconds = InPointSeconds,
            TrimEndSeconds = OutPointSeconds,
            IsLosslessCopy = IsLosslessCopy && !IsCropActive,
            IsCropped = IsCropActive && CropWidth > 0 && CropHeight > 0,
            CropX = CropX,
            CropY = CropY,
            CropWidth = CropWidth,
            CropHeight = CropHeight,
            Status = TranscodeTaskStatus.Pending
        };

        return task;
    }

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static bool TryParseTime(string text, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        var parts = text.Split(':');
        try
        {
            if (parts.Length == 3)
            {
                double h = double.Parse(parts[0]);
                double m = double.Parse(parts[1]);
                double s = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                seconds = h * 3600 + m * 60 + s;
                return true;
            }
            else if (parts.Length == 2)
            {
                double m = double.Parse(parts[0]);
                double s = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                seconds = m * 60 + s;
                return true;
            }
            else if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedSec))
            {
                seconds = parsedSec;
                return true;
            }
        }
        catch { }
        return false;
    }
}
