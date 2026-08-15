using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace RenderPard.UI;

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
    public static event Action<double, long, long>? DownloadProgressChanged; // progress (0.0 to 1.0), bytesRead, totalBytes
    public static event Action<string, string>? UpdateReady; // installerPath, versionTag
    public static event Action<string>? UpdateFailed;

    public static bool IsDownloading { get; private set; }
    public static bool IsUpdateReady { get; private set; }
    public static string? ReadyInstallerPath { get; private set; }
    public static string? LatestVersionTag { get; private set; }

    public static async Task CheckForUpdatesAsync(string owner, string repo, string currentVersionString)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RenderPard", currentVersionString));
            client.Timeout = TimeSpan.FromSeconds(10); 

            string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                return;

            string json = await response.Content.ReadAsStringAsync();
            var latestRelease = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (latestRelease != null && !string.IsNullOrEmpty(latestRelease.TagName))
            {
                string cleanLatest = latestRelease.TagName.TrimStart('v', 'V');
                string cleanCurrent = currentVersionString.TrimStart('v', 'V');

                if (Version.TryParse(cleanLatest, out Version? latestVersion) && 
                    Version.TryParse(cleanCurrent, out Version? currentVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        LatestVersionTag = latestRelease.TagName;

                        // Look for .exe installer asset
                        var exeAsset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string updateMessage = $"Доступно обновление: {latestRelease.Name} ({latestRelease.TagName}).\n\n";
                            if (!string.IsNullOrWhiteSpace(latestRelease.Body))
                            {
                                string bodyText = latestRelease.Body.Length > 400 ? latestRelease.Body.Substring(0, 400) + "..." : latestRelease.Body;
                                updateMessage += $"Что нового:\n{bodyText}\n\n";
                            }
                            
                            if (exeAsset != null)
                            {
                                var result = MessageBox.Show(
                                    updateMessage + "Скачать и подготовить обновление в фоновом режиме прямо сейчас?",
                                    "Обновление RenderPard",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Information);

                                if (result == MessageBoxResult.Yes)
                                {
                                    _ = StartDownloadUpdateAsync(exeAsset.BrowserDownloadUrl, latestRelease.TagName);
                                }
                            }
                            else
                            {
                                var result = MessageBox.Show(
                                    updateMessage + "Перейти на GitHub для скачивания?",
                                    "Обновление RenderPard",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Information);

                                if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(latestRelease.HtmlUrl))
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = latestRelease.HtmlUrl,
                                        UseShellExecute = true
                                    });
                                }
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for updates: {ex.Message}");
        }
    }

    public static async Task StartDownloadUpdateAsync(string downloadUrl, string versionTag)
    {
        if (IsDownloading) return;

        IsDownloading = true;
        IsUpdateReady = false;
        LatestVersionTag = versionTag;

        try
        {
            string tempInstallerPath = Path.Combine(Path.GetTempPath(), $"RenderPard_Setup_{versionTag}.exe");
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(15);

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            using var stream = await response.Content.ReadAsStreamAsync();
            using (var fs = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;
                DateTime lastProgressTime = DateTime.MinValue;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if ((DateTime.Now - lastProgressTime).TotalMilliseconds > 120 || totalRead == totalBytes)
                    {
                        lastProgressTime = DateTime.Now;
                        double progress = totalBytes > 0 ? (double)totalRead / totalBytes : 0;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DownloadProgressChanged?.Invoke(progress, totalRead, totalBytes);
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
        catch (Exception ex)
        {
            IsDownloading = false;
            IsUpdateReady = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateFailed?.Invoke(ex.Message);
            });
        }
    }

    public static void ApplyUpdateAndRestart(string? installerPath = null)
    {
        string path = installerPath ?? ReadyInstallerPath ?? Path.Combine(Path.GetTempPath(), "RenderPard_Setup_Update.exe");
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
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /FORCECLOSEAPPLICATIONS",
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
