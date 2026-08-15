using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RenderPard.Core.Models;

[JsonConverter(typeof(AudioModeJsonConverter))]
public enum AudioMode
{
    Copy,
    Encode,
    None
}

public class AudioModeJsonConverter : JsonConverter<AudioMode>
{
    public override AudioMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.Equals(str, "Copy", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "CopyOrEncode", StringComparison.OrdinalIgnoreCase))
                return AudioMode.Copy;
            if (string.Equals(str, "Encode", StringComparison.OrdinalIgnoreCase))
                return AudioMode.Encode;
            if (string.Equals(str, "None", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(str, "Remove", StringComparison.OrdinalIgnoreCase))
                return AudioMode.None;
        }
        else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int val))
        {
            if (Enum.IsDefined(typeof(AudioMode), val))
                return (AudioMode)val;
        }
        return AudioMode.Copy;
    }

    public override void Write(Utf8JsonWriter writer, AudioMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public enum AudioCodec
{
    Aac,
    Mp3,
    Opus,
    Pcm16,
    Pcm24,
    Flac
}

public enum AudioSampleRate
{
    Original = 0,
    Hz48000 = 48000,
    Hz44100 = 44100
}

public enum AudioChannels
{
    Original = 0,
    Stereo = 2,
    Mono = 1
}

public enum AudioNormalizationTarget
{
    Web,
    Broadcast
}

public enum VideoCodec
{
    H264_Nvenc,
    H265_Nvenc,
    Av1_Nvenc,
    H264_Qsv,
    Hevc_Qsv,
    Av1_Qsv,
    H264_Amf,
    Hevc_Amf,
    Av1_Amf,
    H264,
    H265,
    Av1,
    Vp8,
    Vp9,
    Gif,
    XdcamHd422
}

public enum ContainerFormat
{
    Mp4,
    WebM,
    Gif,
    Jpeg,
    Png,
    Webp,
    MXF,
    Mp3,
    Wav,
    Ogg,
    Flac,
    Aac
}

public enum ResizeMode
{
    Original,
    MaxLongSide,
    ExactWidth,
    ExactHeight,
    Percentage
}

public enum Position9
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight
}

public enum AspectRatioCategory
{
    Landscape, // Width > Height
    Portrait,  // Height > Width
    Square     // Width == Height
}

public enum NamingMode
{
    Suffix,
    Prefix,
    NoChange
}
