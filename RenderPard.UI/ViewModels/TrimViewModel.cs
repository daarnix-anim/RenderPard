using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenderPard.Core;
using RenderPard.Core.Models;

namespace RenderPard.UI.ViewModels;

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

    public ObservableCollection<Preset> AvailablePresets { get; } = new();

    public event Action? RequestPlay;
    public event Action? RequestPause;
    public event Action<double>? RequestSeek;
    public event Action<TranscodeTask, bool>? RequestExport; // task, startImmediately
    public event Action? RequestClose;

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

    partial void OnCurrentTimeSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentTimeText));
    }

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
            IsLosslessCopy = IsLosslessCopy,
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
