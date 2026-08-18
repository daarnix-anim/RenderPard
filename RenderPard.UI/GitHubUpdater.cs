using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace RenderPard.UI;

public enum UpdateCheckStatus
{
    None,
    Checking,
    UpToDate,
    UpdateAvailable,
    Error
}

public class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; set; }
    public GitHubUpdater.GitHubRelease? Release { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class GitHubUpdater
{
    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    // Live progress and readiness events for UI
    public static event Action<double, long, long, double>? DownloadProgressChanged; // progress (0.0 to 1.0), bytesRead, totalBytes, speed (bytes/sec)
    public static event Action<string, string>? UpdateReady; // installerPath, versionTag
    public static event Action<string>? UpdateFailed;
    public static event Action? DownloadCancelled;

    public static bool IsChecking { get; private set; }
    public static bool IsDownloading { get; private set; }
    public static bool IsUpdateReady { get; private set; }
    public static double CurrentProgress { get; private set; }
    public static long CurrentBytesRead { get; private set; }
    public static long CurrentTotalBytes { get; private set; }
    public static double CurrentSpeedBytesPerSec { get; private set; }
    public static string? ReadyInstallerPath { get; private set; }
    public static string? LatestVersionTag { get; private set; }
    public static GitHubRelease? LatestReleaseInfo { get; private set; }

    private static CancellationTokenSource? _downloadCts;
    private static UpdateAvailableDialog? _activeDialog;

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string owner, 
        string repo, 
        string currentVersionString, 
        bool showDialogIfFound = true, 
        Window? ownerWindow = null)
    {
        if (IsChecking)
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Checking,
                CurrentVersion = currentVersionString
            };
        }

        IsChecking = true;
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RenderPard", currentVersionString));
            client.Timeout = TimeSpan.FromSeconds(10); 

            string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                IsChecking = false;
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.Error,
                    CurrentVersion = currentVersionString,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            string json = await response.Content.ReadAsStringAsync();
            var latestRelease = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (latestRelease != null && !string.IsNullOrEmpty(latestRelease.TagName))
            {
                LatestReleaseInfo = latestRelease;
                string cleanLatest = latestRelease.TagName.TrimStart('v', 'V');
                string cleanCurrent = currentVersionString.TrimStart('v', 'V');

                if (Version.TryParse(cleanLatest, out Version? latestVersion) && 
                    Version.TryParse(cleanCurrent, out Version? currentVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        LatestVersionTag = latestRelease.TagName;

                        var exeAsset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                        
                        if (showDialogIfFound)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                OpenUpdateDialog(latestRelease, currentVersionString, exeAsset?.BrowserDownloadUrl, ownerWindow);
                            });
                        }

                        IsChecking = false;
                        return new UpdateCheckResult
                        {
                            Status = UpdateCheckStatus.UpdateAvailable,
                            Release = latestRelease,
                            CurrentVersion = currentVersionString,
                            LatestVersion = latestRelease.TagName
                        };
                    }
                    else
                    {
                        IsChecking = false;
                        return new UpdateCheckResult
                        {
                            Status = UpdateCheckStatus.UpToDate,
                            Release = latestRelease,
                            CurrentVersion = currentVersionString,
                            LatestVersion = latestRelease.TagName
                        };
                    }
                }
            }

            IsChecking = false;
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.UpToDate,
                CurrentVersion = currentVersionString
            };
        }
        catch (Exception ex)
        {
            IsChecking = false;
            Debug.WriteLine($"Error checking for updates: {ex.Message}");
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Error,
                CurrentVersion = currentVersionString,
                ErrorMessage = ex.Message
            };
        }
    }

    public static void OpenUpdateDialog(
        GitHubRelease? release = null, 
        string? currentVersionString = null, 
        string? downloadUrl = null, 
        Window? ownerWindow = null)
    {
        var rel = release ?? LatestReleaseInfo;
        if (rel == null) return;

        if (_activeDialog != null && _activeDialog.IsLoaded)
        {
            _activeDialog.Activate();
            return;
        }

        string curVer = currentVersionString ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var exeAsset = rel.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        string? url = downloadUrl ?? exeAsset?.BrowserDownloadUrl;

        _activeDialog = new UpdateAvailableDialog(
            releaseTitle: rel.Name,
            currentVersion: curVer,
            newVersion: rel.TagName,
            releaseNotes: rel.Body,
            htmlUrl: rel.HtmlUrl,
            downloadUrl: url,
            hasExeAsset: exeAsset != null);

        var targetOwner = ownerWindow ?? (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible ? Application.Current.MainWindow : null);
        if (targetOwner != null && targetOwner.IsLoaded)
        {
            _activeDialog.Owner = targetOwner;
        }

        _activeDialog.Closed += (s, e) => { _activeDialog = null; };
        _activeDialog.Show();
    }

    public static async Task StartDownloadUpdateAsync(string downloadUrl, string versionTag)
    {
        if (IsDownloading) return;

        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        IsDownloading = true;
        IsUpdateReady = false;
        LatestVersionTag = versionTag;
        CurrentProgress = 0;
        CurrentBytesRead = 0;
        CurrentTotalBytes = 0;
        CurrentSpeedBytesPerSec = 0;

        string tempInstallerPath = Path.Combine(Path.GetTempPath(), $"RenderPard_Setup_{versionTag}.exe");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(20);

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            CurrentTotalBytes = totalBytes;

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using (var fs = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;
                
                DateTime lastProgressTime = DateTime.UtcNow;
                long bytesSinceLastTime = 0;
                double currentSpeed = 0;

                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    bytesSinceLastTime += read;

                    var now = DateTime.UtcNow;
                    double elapsedSec = (now - lastProgressTime).TotalSeconds;

                    if (elapsedSec >= 0.15 || totalRead == totalBytes)
                    {
                        if (elapsedSec > 0)
                        {
                            double instantSpeed = bytesSinceLastTime / elapsedSec;
                            currentSpeed = currentSpeed <= 0 ? instantSpeed : (currentSpeed * 0.7 + instantSpeed * 0.3); // Smooth EMA
                        }

                        lastProgressTime = now;
                        bytesSinceLastTime = 0;

                        double progress = totalBytes > 0 ? (double)totalRead / totalBytes : 0;
                        CurrentProgress = progress;
                        CurrentBytesRead = totalRead;
                        CurrentSpeedBytesPerSec = currentSpeed;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DownloadProgressChanged?.Invoke(progress, totalRead, totalBytes, currentSpeed);
                        });
                    }
                }
            }

            IsDownloading = false;
            IsUpdateReady = true;
            ReadyInstallerPath = tempInstallerPath;

            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateReady?.Invoke(tempInstallerPath, versionTag);
            });
        }
        catch (OperationCanceledException)
        {
            IsDownloading = false;
            IsUpdateReady = false;
            try
            {
                if (File.Exists(tempInstallerPath))
                    File.Delete(tempInstallerPath);
            }
            catch { }

            Application.Current.Dispatcher.Invoke(() =>
            {
                DownloadCancelled?.Invoke();
            });
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            IsUpdateReady = false;
            try
            {
                if (File.Exists(tempInstallerPath))
                    File.Delete(tempInstallerPath);
            }
            catch { }

            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateFailed?.Invoke(ex.Message);
            });
        }
    }

    public static void CancelDownload()
    {
        if (IsDownloading)
        {
            _downloadCts?.Cancel();
        }
    }

    public static void ApplyUpdateAndRestart(string? installerPath = null)
    {
        string path = installerPath ?? ReadyInstallerPath ?? Path.Combine(Path.GetTempPath(), $"RenderPard_Setup_{LatestVersionTag ?? "Update"}.exe");
        if (!File.Exists(path))
        {
            MessageBox.Show("Файл установщика не найден. Попробуйте проверить обновления вручную.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "/FORCECLOSEAPPLICATIONS",
                UseShellExecute = true
            };

            Process.Start(processInfo);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось запустить обновление:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
