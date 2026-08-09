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
                loaded.Add(def);
            }
            else
            {
                // Could optionally overwrite existing built-in properties with defaults
                // to ensure updates to the app also update the built-in presets logic.
                // For MVP, just keeping the existing one is fine if user customized `ShowInContextMenu`.
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
                Name = "Web_Split_Alpha",
                IsBuiltIn = true,
                SortOrder = 4,
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
                SortOrder = 5,
                Container = ContainerFormat.Gif,
                VideoCodec = VideoCodec.Gif,
                GifFps = 15,
                MaxLongSideSize = 480,
                TargetVideoBitrateKbps = 0,
                AudioMode = AudioMode.Remove,
                ExtractAlphaMask = false
            }
        };
    }
}
