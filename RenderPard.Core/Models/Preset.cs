using System.Collections.Generic;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace RenderPard.Core.Models;

public class Preset : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public bool ShowInContextMenu { get; set; } = true;
    public string CustomIcon { get; set; } = "Default";
    public int SortOrder { get; set; }

    private bool _keepOriginalResolution;
    public bool KeepOriginalResolution
    {
        get => _keepOriginalResolution;
        set => SetProperty(ref _keepOriginalResolution, value);
    }
    
    private NamingMode _numberingLogic = NamingMode.NoChange;
    public NamingMode NumberingLogic
    {
        get => _numberingLogic;
        set => SetProperty(ref _numberingLogic, value);
    }
    public NamingMode NamingLogic { get; set; } = NamingMode.Suffix;

    private ContainerFormat _container = ContainerFormat.Mp4;
    public ContainerFormat Container
    {
        get => _container;
        set 
        {
            if (SetProperty(ref _container, value))
            {
                OnPropertyChanged(nameof(IsImagePreset));
                OnPropertyChanged(nameof(IsAudioPreset));
                OnPropertyChanged(nameof(IsVideoPreset));
                OnPropertyChanged(nameof(IsMXF));
                if (_container == ContainerFormat.MXF)
                {
                    VideoCodec = VideoCodec.XdcamHd422;
                    AudioCodec = AudioCodec.Pcm24;
                }
                else if (IsAudioPreset)
                {
                    AudioMode = AudioMode.Encode;
                    if (_container == ContainerFormat.Mp3) AudioCodec = AudioCodec.Mp3;
                    else if (_container == ContainerFormat.Wav) AudioCodec = AudioCodec.Pcm16;
                    else if (_container == ContainerFormat.Flac) AudioCodec = AudioCodec.Flac;
                    else if (_container == ContainerFormat.Ogg) AudioCodec = AudioCodec.Opus;
                    else if (_container == ContainerFormat.Aac) AudioCodec = AudioCodec.Aac;
                }
            }
        }
    }

    private VideoCodec _videoCodec = VideoCodec.H264_Nvenc;
    public VideoCodec VideoCodec
    {
        get => _videoCodec;
        set
        {
            if (SetProperty(ref _videoCodec, value))
            {
                if (_videoCodec == VideoCodec.XdcamHd422)
                {
                    Container = ContainerFormat.MXF;
                    AudioCodec = AudioCodec.Pcm24;
                }
                else if (Container == ContainerFormat.MXF)
                {
                    Container = ContainerFormat.Mp4;
                }
            }
        }
    }
    
    // Scale up to this size on the longest side. 1280px by default.
    public int MaxLongSideSize { get; set; } = 1280;
    
    private bool _forceExactLongSide;
    public bool ForceExactLongSide
    {
        get => _forceExactLongSide;
        set => SetProperty(ref _forceExactLongSide, value);
    }
    
    public int TargetVideoBitrateKbps { get; set; } = 2000;
    
    private double _maxDurationSeconds;
    public double MaxDurationSeconds
    {
        get => _maxDurationSeconds;
        set => SetProperty(ref _maxDurationSeconds, value);
    }
    
    private int _maxFps;
    public int MaxFps
    {
        get => _maxFps;
        set => SetProperty(ref _maxFps, value);
    }
    
    private int _gifFps = 15;
    public int GifFps
    {
        get => _gifFps;
        set => SetProperty(ref _gifFps, value);
    }
    
    // Web_pre logic
    private bool _useWebPreLogic;
    public bool UseWebPreLogic
    {
        get => _useWebPreLogic;
        set => SetProperty(ref _useWebPreLogic, value);
    }
    public int MaxSizeMb { get; set; } = 18;
    
    // Split Alpha Logic
    private bool _extractAlphaMask;
    public bool ExtractAlphaMask
    {
        get => _extractAlphaMask;
        set => SetProperty(ref _extractAlphaMask, value);
    }

    // Image Settings
    private int _imageQuality = 80;
    public int ImageQuality
    {
        get => _imageQuality;
        set => SetProperty(ref _imageQuality, value);
    }
    public string FilenamePattern { get; set; } = "{original}_{preset}";
    public ResizeMode ImageResizeMode { get; set; } = ResizeMode.Original;
    public int ImageResizeValue { get; set; } = 1280;

    [JsonIgnore]
    public bool IsImagePreset => Container == ContainerFormat.Jpeg || Container == ContainerFormat.Png || Container == ContainerFormat.Webp;
    
    [JsonIgnore]
    public bool IsAudioPreset => Container == ContainerFormat.Mp3 || Container == ContainerFormat.Wav || Container == ContainerFormat.Ogg || Container == ContainerFormat.Flac || Container == ContainerFormat.Aac;

    [JsonIgnore]
    public bool IsVideoPreset => !IsImagePreset && !IsAudioPreset;
    
    [JsonIgnore]
    public bool IsMXF => Container == ContainerFormat.MXF;

    private AudioMode _audioMode = AudioMode.Copy;
    public AudioMode AudioMode
    {
        get => _audioMode;
        set => SetProperty(ref _audioMode, value);
    }
    
    [JsonIgnore]
    public bool CopyAudio
    {
        get => AudioMode == AudioMode.Copy;
        set
        {
            if (AudioMode != AudioMode.None)
            {
                AudioMode = value ? AudioMode.Copy : AudioMode.Encode;
                OnPropertyChanged(nameof(CopyAudio));
            }
        }
    }

    [JsonIgnore]
    public bool IncludeAudio
    {
        get => AudioMode != AudioMode.None;
        set
        {
            AudioMode = value ? AudioMode.Encode : AudioMode.None;
            OnPropertyChanged(nameof(IncludeAudio));
            OnPropertyChanged(nameof(CopyAudio));
        }
    }
    
    private AudioCodec _audioCodec = AudioCodec.Aac;
    public AudioCodec AudioCodec
    {
        get => _audioCodec;
        set
        {
            if (SetProperty(ref _audioCodec, value))
            {
                if (Container == ContainerFormat.MXF && _audioCodec != AudioCodec.Pcm24)
                {
                    _audioCodec = AudioCodec.Pcm24;
                    OnPropertyChanged(nameof(AudioCodec));
                }
            }
        }
    }

    private int _audioBitrateKbps = 192;
    public int AudioBitrateKbps
    {
        get => _audioBitrateKbps;
        set => SetProperty(ref _audioBitrateKbps, value);
    }

    private AudioSampleRate _audioSampleRate = AudioSampleRate.Hz48000;
    public AudioSampleRate AudioSampleRate
    {
        get => _audioSampleRate;
        set => SetProperty(ref _audioSampleRate, value);
    }

    private AudioChannels _audioChannels = AudioChannels.Stereo;
    public AudioChannels AudioChannels
    {
        get => _audioChannels;
        set => SetProperty(ref _audioChannels, value);
    }
    
    private bool _normalizeAudio;
    public bool NormalizeAudio
    {
        get => _normalizeAudio;
        set => SetProperty(ref _normalizeAudio, value);
    }

    public AudioNormalizationTarget NormalizationTarget { get; set; } = AudioNormalizationTarget.Web;

    private bool _hasWatermark;
    public bool HasWatermark
    {
        get => _hasWatermark;
        set => SetProperty(ref _hasWatermark, value);
    }
    public WatermarkSettings Watermark { get; set; } = new();
    
    private bool _hasTimecode;
    public bool HasTimecode
    {
        get => _hasTimecode;
        set => SetProperty(ref _hasTimecode, value);
    }
    // Different timecode styles based on aspect ratio
    public List<TimecodeStyle> TimecodeStyles { get; set; } = new()
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
            CustomIcon = CustomIcon,
            SortOrder = SortOrder,
            Container = Container,
            VideoCodec = VideoCodec,
            GifFps = GifFps,
            MaxLongSideSize = MaxLongSideSize,
            ForceExactLongSide = ForceExactLongSide,
            MaxDurationSeconds = MaxDurationSeconds,
            MaxFps = MaxFps,
            KeepOriginalResolution = KeepOriginalResolution,
            NumberingLogic = NumberingLogic,
            NamingLogic = NamingLogic,
            TargetVideoBitrateKbps = TargetVideoBitrateKbps,
            UseWebPreLogic = UseWebPreLogic,
            MaxSizeMb = MaxSizeMb,
            ExtractAlphaMask = ExtractAlphaMask,
            ImageQuality = ImageQuality,
            FilenamePattern = FilenamePattern,
            ImageResizeMode = ImageResizeMode,
            ImageResizeValue = ImageResizeValue,
            AudioMode = AudioMode,
            AudioCodec = AudioCodec,
            AudioBitrateKbps = AudioBitrateKbps,
            AudioSampleRate = AudioSampleRate,
            AudioChannels = AudioChannels,
            NormalizeAudio = NormalizeAudio,
            NormalizationTarget = NormalizationTarget,
            HasWatermark = HasWatermark,
            Watermark = Watermark.Clone(),
            HasTimecode = HasTimecode,
            TimecodeStyles = TimecodeStyles.ConvertAll(s => s.Clone())
        };
    }
}
