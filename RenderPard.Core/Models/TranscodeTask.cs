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
}
