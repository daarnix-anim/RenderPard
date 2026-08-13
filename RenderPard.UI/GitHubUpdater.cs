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
                        // Look for .exe installer asset
                        var exeAsset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string updateMessage = $"Доступно обновление: {latestRelease.Name} ({latestRelease.TagName}).\n\n";
                            if (!string.IsNullOrWhiteSpace(latestRelease.Body))
                            {
                                // Show a truncated version of the body if it's too long
                                string bodyText = latestRelease.Body.Length > 500 ? latestRelease.Body.Substring(0, 500) + "..." : latestRelease.Body;
                                updateMessage += $"Что нового:\n{bodyText}\n\n";
                            }
                            
                            if (exeAsset != null)
                            {
                                var result = MessageBox.Show(
                                    updateMessage + "Скачать и установить его в фоновом режиме прямо сейчас?",
                                    "Обновление RenderPard",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Information);

                                if (result == MessageBoxResult.Yes)
                                {
                                    _ = DownloadAndInstallUpdateAsync(exeAsset.BrowserDownloadUrl);
                                }
                            }
                            else
                            {
                                // Fallback if no .exe is attached
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

    private static async Task DownloadAndInstallUpdateAsync(string downloadUrl)
    {
        try
        {
            string tempInstallerPath = Path.Combine(Path.GetTempPath(), "RenderPard_Setup_Update.exe");
            
            using var client = new HttpClient();
            // Need a larger timeout for downloading a 150MB+ file
            client.Timeout = TimeSpan.FromMinutes(10);
            
            // We could show a progress window here, but for now we download silently in the background
            var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            using (var fs = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fs);
            }

            // Launch the installer silently
            var processInfo = new ProcessStartInfo
            {
                FileName = tempInstallerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /FORCECLOSEAPPLICATIONS",
                UseShellExecute = true
            };
            
            Process.Start(processInfo);

            // Shutdown the current application so the installer can overwrite the files
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Ошибка при скачивании или установке обновления:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }
}
