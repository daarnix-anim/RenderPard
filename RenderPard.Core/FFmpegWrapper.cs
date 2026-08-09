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
        sb.Append($"-i \"{task.SourceFilePath}\" ");

        void AppendVideoOptions(StringBuilder b)
        {
            if (task.Preset.Container == ContainerFormat.Gif)
            {
                b.Append("-c:v gif ");
                return;
            }

            if (task.Preset.VideoCodec == VideoCodec.H264_Nvenc)
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

            if (task.Preset.VideoCodec != VideoCodec.Gif)
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
            if (task.Preset.AudioMode == AudioMode.Encode)
            {
                if (task.Preset.AudioCodec == AudioCodec.Aac)
                    b.Append($"-c:a aac -b:a {task.Preset.AudioBitrateKbps}k ");
                else
                    b.Append($"-c:a libopus -b:a {task.Preset.AudioBitrateKbps}k ");
            }
            else
            {
                b.Append("-c:a copy ");
            }
        }

        void AppendContainerOption(StringBuilder b)
        {
            if (task.Preset.Container == ContainerFormat.WebM)
                b.Append("-f webm ");
            else if (task.Preset.Container == ContainerFormat.Gif)
                b.Append("-f gif ");
            else
                b.Append("-f mp4 ");
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
        else if (task.Preset.ExtractAlphaMask)
        {
            string complexFilter = string.IsNullOrEmpty(filterGraph)
                ? "[0:v]split=2[main][alpha];[main]format=yuv420p[out1];[alpha]alphaextract,format=yuv420p[out2]"
                : $"{filterGraph},split=2[main][alpha];[main]format=yuv420p[out1];[alpha]alphaextract,format=yuv420p[out2]";
            
            sb.Append($"-filter_complex \"{complexFilter}\" ");
            
            // 1st Output: Main Video
            sb.Append("-map \"[out1]\" ");
            if (task.HasAudio && task.Preset.AudioMode != AudioMode.Remove)
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

            if (!task.HasAudio || task.Preset.AudioMode == AudioMode.Remove)
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

        // 1. Scale
        if (logicalWidth > 0 && logicalHeight > 0)
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
            if (task.Preset.Container == ContainerFormat.WebM)
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
                        if (task.DurationSeconds > 0)
                        {
                            task.Progress = seconds / task.DurationSeconds;
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
