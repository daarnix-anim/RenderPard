using System;
using System.IO;
using System.Text.Json;

namespace RenderPard.UI
{
    public class AppSettings
    {
        public bool OpenFolderOnCompletion { get; set; } = true;
        public bool MinimizeToTrayOnClose { get; set; } = false;
        public string Language { get; set; } = "ru-RU";
        public bool CreateSubfolderForFiles { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = false;
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 450;
    }

    public static class AppSettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RenderPard", "settings.json");

        public static AppSettings LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch { return new AppSettings(); }
            }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
