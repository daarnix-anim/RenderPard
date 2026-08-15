using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RenderPard.Core.Models;

namespace RenderPard.Core;

public class FFmpegWrapper
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    
    // We assume ffmpeg.exe and ffprobe.exe are in the app directory or system PATH.
    public FFmpegWrapper(string ffmpegPath = "ffmpeg.exe", string ffprobePath = "ffprobe.exe")
    {
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    public async Task<bool> IsNvencAvailableAsync()
    {
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            using var process = new Process();
            process.StartInfo.FileName = _ffmpegPath;
            process.StartInfo.Arguments = "-encoders";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Contains("h264_nvenc") || output.Contains("hevc_nvenc");
        }
        catch
        {
            return false;
        }
    }

    public async Task ProbeTaskAsync(TranscodeTask task)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = _ffprobePath;
            process.StartInfo.Arguments = $"-v quiet -print_format json -show_format -show_streams \"{task.SourceFilePath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            using var doc = JsonDocument.Parse(output);
            var format = doc.RootElement.GetProperty("format");
            
            if (format.TryGetProperty("duration", out var durationProp))
            {
                if (double.TryParse(durationProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                {
                    task.DurationSeconds = dur;
                }
            }

            var streams = doc.RootElement.GetProperty("streams");
            foreach (var stream in streams.EnumerateArray())
            {
                string? codecType = stream.GetProperty("codec_type").GetString();
                if (codecType == "video")
                {
                    task.VideoWidth = stream.GetProperty("width").GetInt32();
                    task.VideoHeight = stream.GetProperty("height").GetInt32();
                    
                    if (stream.TryGetProperty("avg_frame_rate", out var fpsProp))
                    {
                        var fpsParts = fpsProp.GetString()?.Split('/');
                        if (fpsParts.Length == 2 && double.TryParse(fpsParts[0], out double num) && double.TryParse(fpsParts[1], out double den) && den != 0)
                        {
                            task.Fps = num / den;
                        }
                    }

                    if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("rotate", out var rotateProp))
                    {
                        if (int.TryParse(rotateProp.GetString(), out int rotation))
                        {
                            task.Rotation = rotation;
                        }
                    }
                }
                else if (codecType == "audio")
                {
                    task.HasAudio = true;
                }
            }
        }
        catch (Exception ex)
        {
            task.ErrorMessage = $"Failed to probe: {ex.Message}";
            task.Status = TranscodeTaskStatus.Failed;
        }
    }

    public string BuildFfmpegArguments(TranscodeTask task)
    {
        var sb = new StringBuilder();
        sb.Append("-y "); // overwrite
        sb.Append("-progress pipe:2 "); // Progress formatting

        if (task.IsTrimmed)
        {
            if (task.TrimStartSeconds.HasValue && task.TrimStartSeconds.Value > 0)
            {
                sb.Append($"-ss {task.TrimStartSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)} ");
            }
            if (task.TrimEndSeconds.HasValue && task.TrimEndSeconds.Value > (task.TrimStartSeconds ?? 0))
            {
                sb.Append($"-to {task.TrimEndSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)} ");
            }
        }

        sb.Append($"-i \"{task.SourceFilePath}\" ");

        if (task.IsLosslessCopy)
        {
            sb.Append("-c copy ");
            string targetExt = Path.GetExtension(task.TargetFilePath).ToLower();
            if (targetExt is ".mp4" or ".mov" || task.Preset.Container == ContainerFormat.Mp4)
            {
                sb.Append("-movflags +faststart ");
            }
            sb.Append($"\"{task.TargetFilePath}.part\"");
            return sb.ToString();
        }

        void AppendVideoOptions(StringBuilder b)
        {
            if (task.Preset.Container == ContainerFormat.Gif)
            {
                b.Append("-c:v gif ");
                return;
            }
            if (task.Preset.IsImagePreset)
            {
                if (task.Preset.Container == ContainerFormat.Webp)
                    b.Append($"-c:v libwebp -qscale:v {task.Preset.ImageQuality} ");
                else if (task.Preset.Container == ContainerFormat.Jpeg)
                {
                    int qscale = 31 - ((task.Preset.ImageQuality * 29) / 100);
                    b.Append($"-c:v mjpeg -q:v {Math.Max(2, qscale)} ");
                }
                else if (task.Preset.Container == ContainerFormat.Png)
                    b.Append("-c:v png ");
                return;
            }

            if (task.Preset.VideoCodec == VideoCodec.XdcamHd422)
            {
                b.Append("-c:v mpeg2video -pix_fmt yuv422p -r 25 -s 1920x1080 -b:v 50000k -maxrate 50000k -minrate 50000k -bufsize 17000k -flags +ildct+ilme -top 1 -dc 10 -intra_vlc 1 -qmax 3 -lmin \"1*QP2LAMBDA\" -vtag xd5c -color_primaries 1 -color_trc 1 -colorspace 1 ");
            }
            else if (task.Preset.VideoCodec == VideoCodec.H264_Nvenc)
                b.Append("-c:v h264_nvenc -preset p4 -rc vbr ");
            else if (task.Preset.VideoCodec == VideoCodec.H265_Nvenc)
                b.Append("-c:v hevc_nvenc -preset p4 -rc vbr ");
            else if (task.Preset.VideoCodec == VideoCodec.H264)
                b.Append("-c:v libx264 -preset medium ");
            else if (task.Preset.VideoCodec == VideoCodec.H265)
                b.Append("-c:v libx265 -preset medium ");
            else if (task.Preset.VideoCodec == VideoCodec.Vp8)
                b.Append("-c:v libvpx -crf 10 -b:v 1M -auto-alt-ref 0 ");
            else if (task.Preset.VideoCodec == VideoCodec.Vp9)
                b.Append("-c:v libvpx-vp9 -crf 30 -b:v 0 -auto-alt-ref 0 ");
            else if (task.Preset.VideoCodec == VideoCodec.Gif)
                b.Append("-c:v gif ");

            if (task.Preset.VideoCodec != VideoCodec.Gif && task.Preset.VideoCodec != VideoCodec.XdcamHd422)
            {
                int targetBitrate = task.Preset.TargetVideoBitrateKbps;
                if (task.Preset.UseWebPreLogic)
                    targetBitrate = WebPreCalculator.CalculateVideoBitrateKbps(task, task.Preset);

                if (task.Preset.VideoCodec == VideoCodec.H264_Nvenc || task.Preset.VideoCodec == VideoCodec.H265_Nvenc || task.Preset.VideoCodec == VideoCodec.H264 || task.Preset.VideoCodec == VideoCodec.H265)
                {
                    int maxRate = (int)(targetBitrate * 1.2);
                    int bufSize = targetBitrate * 2;
                    b.Append($"-b:v {targetBitrate}k -maxrate {maxRate}k -bufsize {bufSize}k ");
                }
                else if (task.Preset.VideoCodec == VideoCodec.Vp8 || task.Preset.VideoCodec == VideoCodec.Vp9)
                {
                     b.Append($"-b:v {targetBitrate}k ");
                }
            }
        }

        void AppendAudioOptions(StringBuilder b)
        {
            if (task.Preset.IsImagePreset || task.Preset.Container == ContainerFormat.Gif)
            {
                b.Append("-an ");
                return;
            }

            if (task.Preset.IsAudioPreset)
            {
                if (task.Preset.Container == ContainerFormat.Mp3 || task.Preset.AudioCodec == AudioCodec.Mp3)
                {
                    b.Append("-c:a libmp3lame ");
                    int bitrate = task.Preset.AudioBitrateKbps > 0 ? task.Preset.AudioBitrateKbps : 192;
                    b.Append($"-b:a {bitrate}k ");
                }
                else if (task.Preset.Container == ContainerFormat.Wav || task.Preset.AudioCodec == AudioCodec.Pcm16)
                {
                    b.Append("-c:a pcm_s16le ");
                }
                else if (task.Preset.AudioCodec == AudioCodec.Pcm24)
                {
                    b.Append("-c:a pcm_s24le ");
                }
                else if (task.Preset.Container == ContainerFormat.Flac || task.Preset.AudioCodec == AudioCodec.Flac)
                {
                    b.Append("-c:a flac ");
                }
                else if (task.Preset.Container == ContainerFormat.Ogg || task.Preset.AudioCodec == AudioCodec.Opus)
                {
                    b.Append("-c:a libopus ");
                    int bitrate = task.Preset.AudioBitrateKbps > 0 ? task.Preset.AudioBitrateKbps : 128;
                    b.Append($"-b:a {bitrate}k ");
                }
                else if (task.Preset.Container == ContainerFormat.Aac || task.Preset.AudioCodec == AudioCodec.Aac)
                {
                    b.Append("-c:a aac ");
                    int bitrate = task.Preset.AudioBitrateKbps > 0 ? task.Preset.AudioBitrateKbps : 192;
                    b.Append($"-b:a {bitrate}k ");
                }
                else
                {
                    b.Append("-c:a libmp3lame -b:a 192k ");
                }

                if (task.Preset.AudioSampleRate == AudioSampleRate.Hz48000)
                    b.Append("-ar 48000 ");
                else if (task.Preset.AudioSampleRate == AudioSampleRate.Hz44100)
                    b.Append("-ar 44100 ");

                if (task.Preset.AudioChannels == AudioChannels.Stereo)
                    b.Append("-ac 2 ");
                else if (task.Preset.AudioChannels == AudioChannels.Mono)
                    b.Append("-ac 1 ");

                if (task.Preset.NormalizeAudio)
                {
                    if (task.Preset.NormalizationTarget == AudioNormalizationTarget.Web)
                        b.Append("-af \"loudnorm=I=-14:LRA=11:TP=-1.0\" ");
                    else
                        b.Append("-af \"loudnorm=I=-23:LRA=18:TP=-1.0\" ");
                }
                return;
            }

            if (task.Preset.Container == ContainerFormat.MXF || task.Preset.VideoCodec == VideoCodec.XdcamHd422)
            {
                b.Append("-c:a pcm_s24le -ar 48000 ");
                return;
            }

            if (task.Preset.AudioMode == AudioMode.None)
            {
                b.Append("-an ");
                return;
            }

            if (task.Preset.AudioMode == AudioMode.Copy)
            {
                b.Append("-c:a copy ");
                return;
            }

            if (task.Preset.AudioMode == AudioMode.Encode)
            {
                if (task.Preset.AudioCodec == AudioCodec.Aac)
                    b.Append("-c:a aac ");
                else if (task.Preset.AudioCodec == AudioCodec.Mp3)
                    b.Append("-c:a libmp3lame ");
                else if (task.Preset.AudioCodec == AudioCodec.Opus)
                    b.Append("-c:a libopus ");
                else if (task.Preset.AudioCodec == AudioCodec.Pcm16)
                    b.Append("-c:a pcm_s16le ");
                else if (task.Preset.AudioCodec == AudioCodec.Pcm24)
                    b.Append("-c:a pcm_s24le ");
                else if (task.Preset.AudioCodec == AudioCodec.Flac)
                    b.Append("-c:a flac ");
                else
                    b.Append("-c:a aac ");

                if (task.Preset.AudioCodec != AudioCodec.Pcm24 && task.Preset.AudioCodec != AudioCodec.Pcm16 && task.Preset.AudioCodec != AudioCodec.Flac)
                {
                    int bitrate = task.Preset.AudioBitrateKbps > 0 ? task.Preset.AudioBitrateKbps : 192;
                    b.Append($"-b:a {bitrate}k ");
                }

                if (task.Preset.AudioSampleRate == AudioSampleRate.Hz48000)
                    b.Append("-ar 48000 ");
                else if (task.Preset.AudioSampleRate == AudioSampleRate.Hz44100)
                    b.Append("-ar 44100 ");

                if (task.Preset.AudioChannels == AudioChannels.Stereo)
                    b.Append("-ac 2 ");
                else if (task.Preset.AudioChannels == AudioChannels.Mono)
                    b.Append("-ac 1 ");

                if (task.Preset.NormalizeAudio)
                {
                    if (task.Preset.NormalizationTarget == AudioNormalizationTarget.Web)
                        b.Append("-af \"loudnorm=I=-14:LRA=11:TP=-1.0\" ");
                    else
                        b.Append("-af \"loudnorm=I=-23:LRA=18:TP=-1.0\" ");
                }
            }
        }

        void AppendContainerOption(StringBuilder b)
        {
            if (task.Preset.Container == ContainerFormat.WebM)
                b.Append("-f webm ");
            else if (task.Preset.Container == ContainerFormat.Gif)
                b.Append("-f gif ");
            else if (task.Preset.IsImagePreset)
                b.Append("-f image2 ");
            else if (task.Preset.Container == ContainerFormat.MXF)
                b.Append("-f mxf ");
            else if (task.Preset.Container == ContainerFormat.Mp3)
                b.Append("-f mp3 ");
            else if (task.Preset.Container == ContainerFormat.Wav)
                b.Append("-f wav ");
            else if (task.Preset.Container == ContainerFormat.Ogg)
                b.Append("-f ogg ");
            else if (task.Preset.Container == ContainerFormat.Flac)
                b.Append("-f flac ");
            else if (task.Preset.Container == ContainerFormat.Aac)
                b.Append("-f adts ");
            else
                b.Append("-f mp4 ");
        }

        if (task.Preset.IsAudioPreset)
        {
            sb.Append("-vn ");
            AppendAudioOptions(sb);
            AppendContainerOption(sb);
            sb.Append($"\"{task.TargetFilePath}.part\"");
            return sb.ToString();
        }

        string filterGraph = BuildFilterGraph(task);

        if (task.Preset.Container == ContainerFormat.Gif)
        {
            string complexFilter = string.IsNullOrEmpty(filterGraph)
                ? $"[0:v]fps={task.Preset.GifFps},split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse"
                : $"{filterGraph},fps={task.Preset.GifFps},split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse";
            
            sb.Append($"-filter_complex \"{complexFilter}\" ");
            sb.Append("-an "); // GIF has no audio
            AppendVideoOptions(sb);
            AppendContainerOption(sb);
            sb.Append($"\"{task.TargetFilePath}.part\"");
        }
        else if (task.Preset.IsImagePreset)
        {
            sb.Append("-frames:v 1 ");
            AppendVideoOptions(sb);

            if (!string.IsNullOrEmpty(filterGraph))
                sb.Append($"-vf \"{filterGraph}\" ");

            sb.Append("-an ");
            AppendContainerOption(sb);
            sb.Append($"\"{task.TargetFilePath}.part\"");
        }
        else if (task.Preset.ExtractAlphaMask)
        {
            string complexFilter = string.IsNullOrEmpty(filterGraph)
                ? "[0:v]split=2[main][alpha];[main]format=yuv420p[out1];[alpha]alphaextract,format=yuv420p[out2]"
                : $"{filterGraph},split=2[main][alpha];[main]format=yuv420p[out1];[alpha]alphaextract,format=yuv420p[out2]";
            
            sb.Append($"-filter_complex \"{complexFilter}\" ");
            
            // 1st Output: Main Video
            sb.Append("-map \"[out1]\" ");
            if (task.HasAudio && task.Preset.AudioMode != AudioMode.None)
            {
                sb.Append("-map 0:a? ");
                AppendAudioOptions(sb);
            }
            else
            {
                sb.Append("-an ");
            }
            AppendVideoOptions(sb);
            AppendContainerOption(sb);
            sb.Append($"\"{task.TargetFilePath}.part\" ");
            
            // 2nd Output: Alpha Mask
            sb.Append("-map \"[out2]\" -an ");
            AppendVideoOptions(sb);
            AppendContainerOption(sb);
            
            string ext = System.IO.Path.GetExtension(task.TargetFilePath);
            string maskTargetPart = task.TargetFilePath.Substring(0, task.TargetFilePath.Length - ext.Length) + "_mask" + ext + ".part";
            sb.Append($"\"{maskTargetPart}\"");
        }
        else
        {
            AppendVideoOptions(sb);

            if (!string.IsNullOrEmpty(filterGraph))
                sb.Append($"-vf \"{filterGraph}\" ");

            if (!task.HasAudio || task.Preset.AudioMode == AudioMode.None)
                sb.Append("-an ");
            else
                AppendAudioOptions(sb);

            AppendContainerOption(sb);
            sb.Append($"\"{task.TargetFilePath}.part\"");
        }

        return sb.ToString();
    }

    private string BuildFilterGraph(TranscodeTask task)
    {
        var filters = new System.Collections.Generic.List<string>();
        
        // Logical width/height after rotation
        bool isPortraitRotated = task.Rotation == 90 || task.Rotation == -90 || task.Rotation == 270;
        int logicalWidth = isPortraitRotated ? task.VideoHeight : task.VideoWidth;
        int logicalHeight = isPortraitRotated ? task.VideoWidth : task.VideoHeight;

        AspectRatioCategory aspectRatio = AspectRatioCategory.Landscape;
        if (logicalHeight > logicalWidth) aspectRatio = AspectRatioCategory.Portrait;
        else if (logicalHeight == logicalWidth) aspectRatio = AspectRatioCategory.Square;

        // 0. Crop filter (Applied first before scaling and watermark)
        if (task.IsCropped && task.CropWidth > 0 && task.CropHeight > 0)
        {
            int w = Math.Max(2, task.CropWidth & ~1); // ensure even dimensions for YUV420p
            int h = Math.Max(2, task.CropHeight & ~1);
            int x = Math.Max(0, task.CropX & ~1);
            int y = Math.Max(0, task.CropY & ~1);
            filters.Add($"crop={w}:{h}:{x}:{y}");
        }

        // 1. Scale
        if (logicalWidth > 0 && logicalHeight > 0 && !task.Preset.KeepOriginalResolution)
        {
            if (task.Preset.IsImagePreset)
            {
                if (task.Preset.ImageResizeMode == ResizeMode.MaxLongSide && task.Preset.ImageResizeValue > 0)
                {
                    if (aspectRatio == AspectRatioCategory.Landscape && logicalWidth > task.Preset.ImageResizeValue)
                        filters.Add($"scale='min({task.Preset.ImageResizeValue},iw)':-2");
                    else if (aspectRatio == AspectRatioCategory.Portrait && logicalHeight > task.Preset.ImageResizeValue)
                        filters.Add($"scale=-2:'min({task.Preset.ImageResizeValue},ih)'");
                    else if (aspectRatio == AspectRatioCategory.Square && logicalWidth > task.Preset.ImageResizeValue)
                        filters.Add($"scale='min({task.Preset.ImageResizeValue},iw)':-2");
                }
                else if (task.Preset.ImageResizeMode == ResizeMode.ExactWidth && task.Preset.ImageResizeValue > 0)
                {
                    filters.Add($"scale={task.Preset.ImageResizeValue}:-2");
                }
                else if (task.Preset.ImageResizeMode == ResizeMode.ExactHeight && task.Preset.ImageResizeValue > 0)
                {
                    filters.Add($"scale=-2:{task.Preset.ImageResizeValue}");
                }
                else if (task.Preset.ImageResizeMode == ResizeMode.Percentage && task.Preset.ImageResizeValue > 0)
                {
                    filters.Add($"scale=iw*{task.Preset.ImageResizeValue}/100:-2");
                }
            }
            else
            {
                if (aspectRatio == AspectRatioCategory.Landscape && logicalWidth > task.Preset.MaxLongSideSize)
                {
                    filters.Add($"scale='min({task.Preset.MaxLongSideSize},iw)':-2");
                }
                else if (aspectRatio == AspectRatioCategory.Portrait && logicalHeight > task.Preset.MaxLongSideSize)
                {
                    filters.Add($"scale=-2:'min({task.Preset.MaxLongSideSize},ih)'");
                }
                else if (aspectRatio == AspectRatioCategory.Square && logicalWidth > task.Preset.MaxLongSideSize)
                {
                    filters.Add($"scale='min({task.Preset.MaxLongSideSize},iw)':-2");
                }
            }
        }

        // Local helper for positions
        (string x, string y) GetPosition(Position9 pos, int offset)
        {
            return pos switch
            {
                Position9.TopLeft => ($"{offset}", $"{offset}"),
                Position9.TopCenter => ("(w-tw)/2", $"{offset}"),
                Position9.TopRight => ($"w-tw-{offset}", $"{offset}"),
                Position9.MiddleLeft => ($"{offset}", "(h-th)/2"),
                Position9.Center => ("(w-tw)/2", "(h-th)/2"),
                Position9.MiddleRight => ($"w-tw-{offset}", "(h-th)/2"),
                Position9.BottomLeft => ($"{offset}", $"h-th-{offset}"),
                Position9.BottomCenter => ("(w-tw)/2", $"h-th-{offset}"),
                Position9.BottomRight => ($"w-tw-{offset}", $"h-th-{offset}"),
                _ => ("(w-tw)/2", $"h-th-{offset}")
            };
        }

        // 2. Watermark
        if (task.Preset.HasWatermark && task.Preset.Watermark != null && !string.IsNullOrEmpty(task.Preset.Watermark.Text))
        {
            var w = task.Preset.Watermark;
            string colorWithAlpha = w.Color.Replace("#", "0x") + ((int)(w.Opacity * 255)).ToString("X2");
            var (x, y) = GetPosition(w.Position, 20); // 20px offset
            
            // Note: In MVP, using standard Arial to avoid font file paths complexity, but should properly escape text
            string safeText = w.Text.Replace(":", "\\:").Replace("'", "\\'");
            filters.Add($"drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='{safeText}':fontsize={w.FontSize}:fontcolor={colorWithAlpha}:x={x}:y={y}");
        }

        // 3. Timecode
        if (task.Preset.HasTimecode && task.Preset.TimecodeStyles != null && task.Preset.TimecodeStyles.Count > 0)
        {
            // Find style for this aspect ratio
            var style = task.Preset.TimecodeStyles.Find(s => s.AspectRatioRange == aspectRatio) 
                        ?? task.Preset.TimecodeStyles[0];
            
            string colorWithAlpha = style.Color.Replace("#", "0x") + ((int)(style.Opacity * 255)).ToString("X2");
            var (x, y) = GetPosition(style.Position, 40); // 40px offset
            
            string fpsString = Math.Max(25, task.Fps).ToString(CultureInfo.InvariantCulture);
            filters.Add($"drawtext=fontfile='C\\:/Windows/Fonts/consola.ttf':timecode='00\\:00\\:00\\:00':rate={fpsString}:fontsize={style.FontSize}:fontcolor={colorWithAlpha}:x={x}:y={y}:box=1:boxcolor=0x00000044:boxborderw=5");
        }

        // 4. Format (Pixel Format)
        if (!task.Preset.ExtractAlphaMask)
        {
            if (task.Preset.IsImagePreset)
            {
                // no format forcing for images to allow natural formats (RGB, RGBA)
            }
            else if (task.Preset.Container == ContainerFormat.WebM)
            {
                // Preserve alpha if present for WebM (VP8/VP9)
                filters.Add("format=yuva420p|yuv420p");
            }
            else
            {
                // Force standard yuv420p for MP4 (H264/H265) to maximize compatibility and prevent crashes with unsupported formats like yuva444p10le
                filters.Add("format=yuv420p");
            }
        }

        return string.Join(",", filters);
    }

    public async Task RunEncodeAsync(TranscodeTask task, CancellationToken cancellationToken)
    {
        if (task.Status == TranscodeTaskStatus.Cancelled || cancellationToken.IsCancellationRequested)
            return;

        task.Status = TranscodeTaskStatus.Encoding;
        
        string args = BuildFfmpegArguments(task);

        using var process = new Process();
        process.StartInfo.FileName = _ffmpegPath;
        process.StartInfo.Arguments = args;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true; // FFmpeg outputs progress to stderr even with -progress pipe:2 usually if not redirected specifically, but -progress pipe:2 writes to stderr.
        process.StartInfo.CreateNoWindow = true;

        var errorLog = new System.Collections.Generic.Queue<string>(5);
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Parse out_time_ms from ffmpeg progress
                if (e.Data.StartsWith("out_time_ms="))
                {
                    if (long.TryParse(e.Data.Substring(12), out long microseconds))
                    {
                        double seconds = microseconds / 1000000.0;
                        double targetDuration = task.EffectiveDurationSeconds > 0 ? task.EffectiveDurationSeconds : task.DurationSeconds;
                        if (targetDuration > 0)
                        {
                            task.Progress = Math.Min(1.0, seconds / targetDuration);
                        }
                    }
                }
                else if (!e.Data.StartsWith("frame=") && !e.Data.StartsWith("bitrate=") && !e.Data.StartsWith("total_size=") && !e.Data.StartsWith("out_time=") && !e.Data.StartsWith("dup_frames=") && !e.Data.StartsWith("drop_frames=") && !e.Data.StartsWith("speed=") && !e.Data.StartsWith("progress="))
                {
                    // Keep the last 5 relevant error lines
                    if (errorLog.Count >= 5) errorLog.Dequeue();
                    errorLog.Enqueue(e.Data);
                }
            }
        };

        try
        {
            process.Start();
            process.BeginErrorReadLine();
            
            // Await with cancellation
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                // Rename part file to final
                if (File.Exists(task.TargetFilePath + ".part"))
                {
                    File.Move(task.TargetFilePath + ".part", task.TargetFilePath, true);
                }

                if (task.Preset.ExtractAlphaMask)
                {
                    string ext = Path.GetExtension(task.TargetFilePath);
                    string maskTarget = task.TargetFilePath.Substring(0, task.TargetFilePath.Length - ext.Length) + "_mask" + ext;
                    if (File.Exists(maskTarget + ".part"))
                    {
                        File.Move(maskTarget + ".part", maskTarget, true);
                    }
                }
                
                task.Status = TranscodeTaskStatus.Completed;
                task.Progress = 1.0;
            }
            else
            {
                task.Status = TranscodeTaskStatus.Failed;
                string lastError = errorLog.Count > 0 ? string.Join(" | ", errorLog) : $"code {process.ExitCode}";
                task.ErrorMessage = $"FFmpeg exited with {lastError}";
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
            task.Status = TranscodeTaskStatus.Cancelled;
        }
        catch (Exception ex)
        {
            task.Status = TranscodeTaskStatus.Failed;
            task.ErrorMessage = ex.Message;
        }
    }
}
