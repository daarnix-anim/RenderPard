using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenderPard.Core.Models;

public enum TranscodeTaskStatus
{
    Pending,
    Probing,
    Encoding,
    Completed,
    Failed,
    Cancelled
}

public partial class TranscodeTask : ObservableObject
{
    [ObservableProperty]
    private string _sourceFilePath = string.Empty;
    
    public string FileName => string.IsNullOrEmpty(OriginalFileName) ? Path.GetFileName(SourceFilePath) : OriginalFileName;

    public string OriginalFileName { get; set; } = string.Empty;
    
    public bool IsTempSource { get; set; } = false;

    [ObservableProperty]
    private string _targetFilePath = string.Empty;

    [ObservableProperty]
    private Preset _preset = new();

    [ObservableProperty]
    private TranscodeTaskStatus _status = TranscodeTaskStatus.Pending;

    [ObservableProperty]
    private double _progress; // 0.0 to 1.0

    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    // Extracted metadata
    public int VideoWidth { get; set; }
    public int VideoHeight { get; set; }
    public double DurationSeconds { get; set; }
    public bool HasAudio { get; set; }
    public double Fps { get; set; }
    public int Rotation { get; set; }

    // Trimming & Cutting properties
    [ObservableProperty]
    private double? _trimStartSeconds;

    [ObservableProperty]
    private double? _trimEndSeconds;

    [ObservableProperty]
    private bool _isLosslessCopy;

    public bool IsTrimmed => TrimStartSeconds.HasValue || TrimEndSeconds.HasValue;

    public double EffectiveDurationSeconds
    {
        get
        {
            if (IsTrimmed)
            {
                double start = TrimStartSeconds ?? 0;
                double end = TrimEndSeconds ?? (DurationSeconds > 0 ? DurationSeconds : start + 1);
                return Math.Max(0.1, end - start);
            }
            return DurationSeconds;
        }
    }

    public string TrimSummary
    {
        get
        {
            if (!IsTrimmed) return string.Empty;
            string startStr = TrimStartSeconds.HasValue ? System.TimeSpan.FromSeconds(TrimStartSeconds.Value).ToString(@"mm\:ss\.f") : "00:00.0";
            string endStr = TrimEndSeconds.HasValue ? System.TimeSpan.FromSeconds(TrimEndSeconds.Value).ToString(@"mm\:ss\.f") : (DurationSeconds > 0 ? System.TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss\.f") : "Конец");
            return $"[{startStr} - {endStr}]";
        }
    }

    // Video Crop & Framing properties
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

    public string CropSummary => IsCropped && CropWidth > 0 && CropHeight > 0 ? $"[{CropWidth}×{CropHeight}]" : string.Empty;
}
