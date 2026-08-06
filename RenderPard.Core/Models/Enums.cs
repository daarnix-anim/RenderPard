namespace RenderPard.Core.Models;

public enum AudioMode
{
    CopyOrEncode, // The spec says "сохранить/перекодировать аудио". We can just encode it by default to chosen codec if not copying. Actually let's do:
    Encode,       // Re-encode to chosen codec
    Remove        // Убрать аудио полностью
}

public enum AudioCodec
{
    Aac,
    Opus
}

public enum VideoCodec
{
    H264_Nvenc,
    H265_Nvenc,
    H264,
    H265,
    Vp8,
    Vp9
}

public enum ContainerFormat
{
    Mp4,
    WebM
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
