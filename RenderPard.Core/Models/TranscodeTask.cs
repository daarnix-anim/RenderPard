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
    public string SourceVideoCodec { get; set; } = string.Empty;
    public string SourceAudioCodec { get; set; } = string.Empty;
    public int SourceVideoBitrateKbps { get; set; }
    public string SourceContainer { get; set; } = string.Empty;

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

    public System.Collections.Generic.List<TrimSegment>? Segments { get; set; }

    public bool IsMultiSegmentMerge => Segments != null && Segments.Count > 1;

    public bool IsTrimmed => IsMultiSegmentMerge || TrimStartSeconds.HasValue || TrimEndSeconds.HasValue;

    public double EffectiveDurationSeconds
    {
        get
        {
            double dur;
            if (IsMultiSegmentMerge && Segments != null)
            {
                double total = 0;
                foreach (var seg in Segments)
                {
                    total += seg.DurationSeconds;
                }
                dur = Math.Max(0.1, total);
            }
            else if (IsTrimmed)
            {
                double start = TrimStartSeconds ?? 0;
                double end = TrimEndSeconds ?? (DurationSeconds > 0 ? DurationSeconds : start + 1);
                dur = Math.Max(0.1, end - start);
            }
            else
            {
                dur = DurationSeconds;
            }

            if (Preset != null && Preset.MaxDurationSeconds > 0 && dur > Preset.MaxDurationSeconds)
            {
                return Preset.MaxDurationSeconds;
            }
            return dur;
        }
    }

    public string TrimSummary
    {
        get
        {
            if (IsMultiSegmentMerge && Segments != null)
            {
                return $"[{Segments.Count} фрагм. : {EffectiveDurationSeconds:F1}с]";
            }
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
