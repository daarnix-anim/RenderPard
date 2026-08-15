using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenderPard.Core.Models;

public partial class TrimSegment : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "Фрагмент";

    [ObservableProperty]
    private double _startSeconds;

    [ObservableProperty]
    private double _endSeconds;

    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);

    public string DurationText => $"{DurationSeconds:F1}с";

    public string FormattedRange => $"{FormatTime(StartSeconds)} — {FormatTime(EndSeconds)}";

    // Crop settings for this specific segment
    [ObservableProperty]
    private bool _isCropped;

    [ObservableProperty]
    private int _cropX;

    [ObservableProperty]
    private int _cropY;

    [ObservableProperty]
    private int _cropWidth;

    [ObservableProperty]
    private int _cropHeight;

    partial void OnStartSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(FormattedRange));
    }

    partial void OnEndSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(FormattedRange));
    }

    private static string FormatTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}";
    }
}
