using System.Collections.Generic;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace RenderPard.Core.Models;

public class Preset : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public bool ShowInContextMenu { get; set; } = true;
    public int SortOrder { get; set; }

    private ContainerFormat _container = ContainerFormat.Mp4;
    public ContainerFormat Container
    {
        get => _container;
        set => SetProperty(ref _container, value);
    }
    public VideoCodec VideoCodec { get; set; } = VideoCodec.H264_Nvenc;
    
    // Scale up to this size on the longest side. 1280px by default.
    public int MaxLongSideSize { get; set; } = 1280;
    
    public int TargetVideoBitrateKbps { get; set; } = 2000;
    
    private int _gifFps = 15;
    public int GifFps
    {
        get => _gifFps;
        set => SetProperty(ref _gifFps, value);
    }
    
    // Web_pre logic
    public bool UseWebPreLogic { get; set; }
    public int MaxSizeMb { get; set; } = 18;
    
    // Split Alpha Logic
    public bool ExtractAlphaMask { get; set; }

    public AudioMode AudioMode { get; set; } = AudioMode.Encode;
    public AudioCodec AudioCodec { get; set; } = AudioCodec.Aac;
    public int AudioBitrateKbps { get; set; } = 128;

    public bool HasWatermark { get; set; }
    public WatermarkSettings Watermark { get; set; } = new();
    
    public bool HasTimecode { get; set; }
    // Different timecode styles based on aspect ratio
    public List<TimecodeStyle> TimecodeStyles { get; set; } = new List<TimecodeStyle>
    {
        new TimecodeStyle { AspectRatioRange = AspectRatioCategory.Landscape },
        new TimecodeStyle { AspectRatioRange = AspectRatioCategory.Portrait },
        new TimecodeStyle { AspectRatioRange = AspectRatioCategory.Square }
    };

    public Preset Clone()
    {
        return new Preset
        {
            Name = Name + " (Copy)",
            IsBuiltIn = false,
            ShowInContextMenu = ShowInContextMenu,
            SortOrder = SortOrder,
            Container = Container,
            VideoCodec = VideoCodec,
            GifFps = GifFps,
            MaxLongSideSize = MaxLongSideSize,
            TargetVideoBitrateKbps = TargetVideoBitrateKbps,
            UseWebPreLogic = UseWebPreLogic,
            MaxSizeMb = MaxSizeMb,
            ExtractAlphaMask = ExtractAlphaMask,
            AudioMode = AudioMode,
            AudioCodec = AudioCodec,
            AudioBitrateKbps = AudioBitrateKbps,
            HasWatermark = HasWatermark,
            HasTimecode = HasTimecode,
            Watermark = Watermark.Clone(),
            TimecodeStyles = TimecodeStyles.ConvertAll(t => t.Clone())
        };
    }
}
