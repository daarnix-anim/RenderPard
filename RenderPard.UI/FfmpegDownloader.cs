using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RenderPard.UI;

public class FfmpegDownloader
{
    private const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    public async Task DownloadAndExtractAsync(string targetDirectory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        string tempZipPath = Path.Combine(targetDirectory, "ffmpeg.zip");
        
        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;

            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var totalRead = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                do
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        totalRead += read;

                        if (canReportProgress)
                        {
                            progress.Report((double)totalRead / totalBytes);
                        }
                    }
                }
                while (isMoreToRead);
            }

            // Extract specific files
            if (progress != null) progress.Report(1.0); // Extraction started
            
            using var archive = ZipFile.OpenRead(tempZipPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) || 
                    entry.FullName.EndsWith("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string destinationPath = Path.Combine(targetDirectory, entry.Name);
                    // Overwrite if exists
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch { }
            }
        }
    }

    public static bool CheckFfmpegExists(string directory)
    {
        if (File.Exists(Path.Combine(directory, "ffmpeg.exe")) && 
            File.Exists(Path.Combine(directory, "ffprobe.exe")))
        {
            return true;
        }

        return IsFfmpegInPath();
    }

    private static bool IsFfmpegInPath()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable)) return false;

        var paths = pathVariable.Split(Path.PathSeparator);
        bool hasFfmpeg = false;
        bool hasFfprobe = false;

        foreach (var path in paths)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var cleanPath = path.Trim('"');
                
                if (!hasFfmpeg && File.Exists(Path.Combine(cleanPath, "ffmpeg.exe")))
                    hasFfmpeg = true;
                
                if (!hasFfprobe && File.Exists(Path.Combine(cleanPath, "ffprobe.exe")))
                    hasFfprobe = true;

                if (hasFfmpeg && hasFfprobe) return true;
            }
            catch
            {
                // Ignore paths that cause access exceptions
            }
        }
        return false;
    }
}
