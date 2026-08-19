using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RenderPard.Core.Models;

namespace RenderPard.Core;

public class PresetManager
{
    private readonly string _presetsFilePath;

    public PresetManager()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "RenderPard");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        _presetsFilePath = Path.Combine(appFolder, "presets.json");
    }

    public List<Preset> LoadPresets()
    {
        if (File.Exists(_presetsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_presetsFilePath);
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };
                var loaded = JsonSerializer.Deserialize<List<Preset>>(json, options);
                if (loaded != null)
                {
                    MergeBuiltInPresets(loaded);
                    return loaded.OrderBy(p => p.SortOrder).ToList();
                }
            }
            catch
            {
                // On error, fall back to default
            }
        }
        
        var defaults = GetDefaultPresets();
        SavePresets(defaults);
        return defaults;
    }

    public void SavePresets(List<Preset> presets)
    {
        for (int i = 0; i < presets.Count; i++)
        {
            presets[i].SortOrder = i;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var json = JsonSerializer.Serialize(presets, options);
        File.WriteAllText(_presetsFilePath, json);
    }

    private void MergeBuiltInPresets(List<Preset> loaded)
    {
        var defaults = GetDefaultPresets();
        foreach (var def in defaults)
        {
            var existing = loaded.FirstOrDefault(p => p.Name == def.Name && p.IsBuiltIn);
            if (existing == null)
            {
                def.SortOrder = loaded.Count > 0 ? loaded.Max(p => p.SortOrder) + 1 : 0;
                loaded.Add(def);
            }
            else
            {
                // If existing built-in preset has legacy "Default" or empty icon, update it to clean default icon
                if (string.IsNullOrEmpty(existing.CustomIcon) || existing.CustomIcon == "Default")
                {
                    existing.CustomIcon = def.CustomIcon;
                }
            }
        }

        // Also fix any user preset with legacy "Default" icon
        foreach (var p in loaded)
        {
            if (p.CustomIcon == "Default")
            {
                if (p.IsImagePreset) p.CustomIcon = "image";
                else if (p.IsAudioPreset) p.CustomIcon = "audio";
                else p.CustomIcon = "video";
            }
        }
    }

    public List<Preset> GetDefaultPresets()
    {
        return new List<Preset>
        {
            new Preset
            {
                Name = "Telegram",
                IsBuiltIn = true,
                SortOrder = 0,
                CustomIcon = "telegram",
                Container = ContainerFormat.Mp4,
                VideoCodec = VideoCodec.H264_Nvenc,
                MaxLongSideSize = 1280,
                TargetVideoBitrateKbps = 2000,
                AudioMode = AudioMode.Encode,
                AudioCodec = AudioCodec.Aac,
                AudioBitrateKbps = 128
            },
            new Preset
            {
                Name = "Demo",
                IsBuiltIn = true,
                SortOrder = 1,
                CustomIcon = "play",
                Container = ContainerFormat.Mp4,
                VideoCodec = VideoCodec.H264_Nvenc,
                MaxLongSideSize = 1280,
                TargetVideoBitrateKbps = 2000,
                AudioMode = AudioMode.Encode,
                AudioCodec = AudioCodec.Aac,
                AudioBitrateKbps = 128,
                HasTimecode = true,
                HasWatermark = true,
                TimecodeStyles = new List<TimecodeStyle>
                {
                    new TimecodeStyle { AspectRatioRange = AspectRatioCategory.Landscape },
                    new TimecodeStyle { AspectRatioRange = AspectRatioCategory.Portrait },
                    new TimecodeStyle { AspectRatioRange = AspectRatioCategory.Square }
                },
                Watermark = new WatermarkSettings
                {
                    Text = "RenderPard Demo" // Defaults to something to show off the feature
                }
            },
            new Preset
            {
                Name = "Web_pre",
                IsBuiltIn = true,
                SortOrder = 2,
                CustomIcon = "video",
                Container = ContainerFormat.Mp4,
                VideoCodec = VideoCodec.H264_Nvenc,
                UseWebPreLogic = true,
                MaxSizeMb = 18,
                TargetVideoBitrateKbps = 2000,
                AudioMode = AudioMode.Encode,
                AudioCodec = AudioCodec.Aac,
                AudioBitrateKbps = 128
            },
            new Preset
            {
                Name = "Web_Alpha",
                IsBuiltIn = true,
                SortOrder = 3,
                CustomIcon = "sparkles",
                Container = ContainerFormat.WebM,
                VideoCodec = VideoCodec.Vp9,
                MaxLongSideSize = 1280,
                TargetVideoBitrateKbps = 2000,
                AudioMode = AudioMode.Encode,
                AudioCodec = AudioCodec.Opus, // Opus is standard for WebM
                AudioBitrateKbps = 128
            },
            new Preset
            {
                Name = "TGstick",
                IsBuiltIn = true,
                SortOrder = 4,
                CustomIcon = "telegram",
                Container = ContainerFormat.WebM,
                VideoCodec = VideoCodec.Vp9,
                MaxLongSideSize = 512,
                ForceExactLongSide = true,
                MaxDurationSeconds = 3.0,
                MaxFps = 30,
                TargetVideoBitrateKbps = 450,
                AudioMode = AudioMode.None
            },
            new Preset
            {
                Name = "Web_Split_Alpha",
                IsBuiltIn = true,
                SortOrder = 5,
                CustomIcon = "cut",
                Container = ContainerFormat.Mp4,
                VideoCodec = VideoCodec.H265_Nvenc,
                MaxLongSideSize = 2160,
                TargetVideoBitrateKbps = 10000,
                ExtractAlphaMask = true,
                AudioMode = AudioMode.Encode,
                AudioCodec = AudioCodec.Aac,
                AudioBitrateKbps = 128
            },
            new Preset
            {
                Name = "GIF (Web)",
                IsBuiltIn = true,
                SortOrder = 6,
                CustomIcon = "fmt_gif",
                Container = ContainerFormat.Gif,
                VideoCodec = VideoCodec.Gif,
                GifFps = 15,
                MaxLongSideSize = 480,
                TargetVideoBitrateKbps = 0,
                AudioMode = AudioMode.None,
                ExtractAlphaMask = false
            },
            new Preset
            {
                Name = "Фото в JPEG",
                IsBuiltIn = true,
                SortOrder = 7,
                CustomIcon = "fmt_jpeg",
                Container = ContainerFormat.Jpeg,
                ImageQuality = 90
            },
            new Preset
            {
                Name = "Фото в WebP (Для Web)",
                IsBuiltIn = true,
                SortOrder = 8,
                CustomIcon = "fmt_webp",
                Container = ContainerFormat.Webp,
                ImageQuality = 80
            },
            new Preset
            {
                Name = "Извлечь / Аудио в MP3",
                IsBuiltIn = true,
                SortOrder = 9,
                Container = ContainerFormat.Mp3,
                AudioCodec = AudioCodec.Mp3,
                AudioBitrateKbps = 192,
                AudioSampleRate = AudioSampleRate.Hz48000,
                AudioChannels = AudioChannels.Stereo,
                CustomIcon = "fmt_mp3"
            },
            new Preset
            {
                Name = "Голосовые в MP3 (128 kbps)",
                IsBuiltIn = true,
                SortOrder = 9,
                Container = ContainerFormat.Mp3,
                AudioCodec = AudioCodec.Mp3,
                AudioBitrateKbps = 128,
                AudioSampleRate = AudioSampleRate.Hz44100,
                AudioChannels = AudioChannels.Mono,
                CustomIcon = "fmt_mp3"
            },
            new Preset
            {
                Name = "Аудио в MP3 HQ (320 kbps)",
                IsBuiltIn = true,
                SortOrder = 10,
                Container = ContainerFormat.Mp3,
                AudioCodec = AudioCodec.Mp3,
                AudioBitrateKbps = 320,
                AudioSampleRate = AudioSampleRate.Hz48000,
                AudioChannels = AudioChannels.Stereo,
                CustomIcon = "fmt_mp3"
            },
            new Preset
            {
                Name = "Аудио в WAV (PCM 16-bit)",
                IsBuiltIn = true,
                SortOrder = 11,
                Container = ContainerFormat.Wav,
                AudioCodec = AudioCodec.Pcm16,
                AudioSampleRate = AudioSampleRate.Hz48000,
                AudioChannels = AudioChannels.Stereo,
                CustomIcon = "fmt_wav"
            }
        };
    }
}
