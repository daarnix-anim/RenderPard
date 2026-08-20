using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using System.Linq;
using RenderPard.Core.Models;

namespace RenderPard.Core;

[SupportedOSPlatform("windows")]
public static class ContextMenuManager
{
    public static readonly string[] VideoExtensions = { ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm", ".wmv", ".flv", ".ts", ".mts", ".m2ts", ".3gp" };
    public static readonly string[] AudioExtensions = { ".wav", ".wave", ".ogg", ".opus", ".m4a", ".aac", ".flac", ".aif", ".aiff", ".aifc", ".amr", ".3ga", ".caf", ".wma", ".weba", ".mp2", ".ac3", ".alac", ".ape", ".wv", ".mp3" };
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".ai", ".pdf", ".heic", ".cr2", ".nef", ".arw", ".dng" };
    public static readonly string[] SupportedExtensions = VideoExtensions.Concat(AudioExtensions).Concat(ImageExtensions).Distinct().ToArray();
    private const string MenuName = "RenderPard";

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;

    public static void NotifyShell()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    public static void Register(List<Preset> presets)
    {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(exePath)) return;

        foreach (var ext in SupportedExtensions)
        {
            RegisterForExtension(ext, presets, exePath);
        }

        RegisterDirectoryMenu(presets, exePath);

        NotifyShell();
    }

    public static void RefreshIfRegistered(List<Preset>? presets = null)
    {
        if (IsRegistered())
        {
            var pList = presets ?? new PresetManager().LoadPresets();
            Register(pList);
        }
    }

    public static void Unregister()
    {
        foreach (var ext in SupportedExtensions)
        {
            UnregisterForExtension(ext);
        }

        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\shell\{MenuName}", false); } catch { }

        NotifyShell();
    }

    public static bool IsWindows11()
    {
        try
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
        }
        catch
        {
            return false;
        }
    }

    private const string Win11ClassicMenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
    private const string Win11ClassicMenuRootKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

    public static bool IsWin11ClassicMenuEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Win11ClassicMenuKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetWin11ClassicMenuEnabled(bool enable, bool restartExplorer = true)
    {
        try
        {
            if (enable)
            {
                using var key = Registry.CurrentUser.CreateSubKey(Win11ClassicMenuKey);
                key?.SetValue("", "");
            }
            else
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(Win11ClassicMenuRootKey, false);
                }
                catch { }
            }

            NotifyShell();

            if (restartExplorer)
            {
                RestartExplorer();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to toggle Win11 classic menu: {ex.Message}");
        }
    }

    public static void RestartExplorer()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c taskkill /f /im explorer.exe & start explorer.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }
        catch { }
    }

    public static bool IsRegistered()
    {
        try
        {
            using var key1 = Registry.CurrentUser.OpenSubKey($@"Software\Classes\SystemFileAssociations\.mp4\shell\{MenuName}_Video");
            using var key2 = Registry.CurrentUser.OpenSubKey($@"Software\Classes\SystemFileAssociations\.mp3\shell\{MenuName}_Audio");
            using var key3 = Registry.CurrentUser.OpenSubKey($@"Software\Classes\SystemFileAssociations\.jpg\shell\{MenuName}_Image");
            return key1 != null || key2 != null || key3 != null;
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterForExtension(string ext, List<Preset> presets, string exePath)
    {
        // First, clean up the old legacy menu just in case
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}", false); } catch { }

        bool isVideo = VideoExtensions.Contains(ext);
        bool isAudio = AudioExtensions.Contains(ext);
        bool isImage = ImageExtensions.Contains(ext);

        // Force Windows to recognize the extension so SystemFileAssociations works even if no default app is set
        try
        {
            using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}"))
            {
                if (extKey != null)
                {
                    if (isImage) extKey.SetValue("PerceivedType", "image");
                    else if (isAudio) extKey.SetValue("PerceivedType", "audio");
                    else if (isVideo) extKey.SetValue("PerceivedType", "video");
                }
            }
        }
        catch { }

        var videoPresets = presets.Where(p => p.ShowInContextMenu && (p.IsVideoPreset || p.IsAudioPreset)).ToList();
        var audioPresets = presets.Where(p => p.ShowInContextMenu && p.IsAudioPreset).ToList();
        var imagePresets = presets.Where(p => p.ShowInContextMenu && p.IsImagePreset).ToList();

        if (isVideo && videoPresets.Any())
            RegisterSubMenu(ext, $"{MenuName}_Video", "RenderPard 🎬", videoPresets, exePath);
        else
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Video", false); } catch { }

        if (isAudio && audioPresets.Any())
            RegisterSubMenu(ext, $"{MenuName}_Audio", "RenderPard 🎵", audioPresets, exePath);
        else
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Audio", false); } catch { }

        if (isImage && imagePresets.Any())
            RegisterSubMenu(ext, $"{MenuName}_Image", "RenderPard 🖼", imagePresets, exePath);
        else
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Image", false); } catch { }
    }

    public static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                // SystemUsesLightTheme determines the context menu and taskbar background color in Windows 10/11
                var sysVal = key.GetValue("SystemUsesLightTheme");
                if (sysVal is int sysInt)
                    return sysInt == 1;

                var appVal = key.GetValue("AppsUseLightTheme");
                if (appVal is int appInt)
                    return appInt == 1;
            }
        }
        catch { }
        return false;
    }

    public static string ResolveThemeIconPath(string rawIconName, string iconsDir, string customIconsDir, bool isLightTheme)
    {
        if (string.IsNullOrWhiteSpace(rawIconName)) return string.Empty;

        // If it's already an absolute path
        if (Path.IsPathRooted(rawIconName) && File.Exists(rawIconName))
        {
            string dir = Path.GetDirectoryName(rawIconName) ?? "";
            string fname = Path.GetFileNameWithoutExtension(rawIconName);
            string ext = Path.GetExtension(rawIconName);

            if (isLightTheme)
            {
                string darkPath = Path.Combine(dir, fname + "_dark" + ext);
                if (File.Exists(darkPath)) return darkPath;
            }
            else
            {
                if (fname.EndsWith("_dark", StringComparison.OrdinalIgnoreCase))
                {
                    string lightPath = Path.Combine(dir, fname.Substring(0, fname.Length - 5) + ext);
                    if (File.Exists(lightPath)) return lightPath;
                }
            }
            return rawIconName;
        }

        // Clean icon key (e.g. "telegram" or "telegram_dark" or "fmt_mp3.ico")
        string cleanName = rawIconName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            ? rawIconName.Substring(0, rawIconName.Length - 4)
            : rawIconName;

        string targetName = cleanName;
        if (isLightTheme)
        {
            if (!cleanName.EndsWith("_dark", StringComparison.OrdinalIgnoreCase))
            {
                targetName = cleanName + "_dark";
            }
        }
        else
        {
            if (cleanName.EndsWith("_dark", StringComparison.OrdinalIgnoreCase))
            {
                targetName = cleanName.Substring(0, cleanName.Length - 5);
            }
        }

        // Search priority:
        // 1. Target theme in customIconsDir (.ico)
        string p1 = Path.Combine(customIconsDir, targetName + ".ico");
        if (File.Exists(p1)) return p1;

        // 2. Target theme in iconsDir (.ico)
        string p2 = Path.Combine(iconsDir, targetName + ".ico");
        if (File.Exists(p2)) return p2;

        // 3. Fallback to cleanName in customIconsDir (.ico)
        string p3 = Path.Combine(customIconsDir, cleanName + ".ico");
        if (File.Exists(p3)) return p3;

        // 4. Fallback to cleanName in iconsDir (.ico)
        string p4 = Path.Combine(iconsDir, cleanName + ".ico");
        if (File.Exists(p4)) return p4;

        // 5. Direct file in customIconsDir (e.g. custom name or extension)
        string p5 = Path.Combine(customIconsDir, rawIconName);
        if (File.Exists(p5)) return p5;

        return string.Empty;
    }

    private static void RegisterDirectoryMenu(List<Preset> presets, string exePath)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\shell\{MenuName}", false); } catch { }

        var videoPresets = presets.Where(p => p.ShowInContextMenu && (p.IsVideoPreset || p.IsAudioPreset)).ToList();
        var audioPresets = presets.Where(p => p.ShowInContextMenu && p.IsAudioPreset).ToList();
        var imagePresets = presets.Where(p => p.ShowInContextMenu && p.IsImagePreset).ToList();

        if (!videoPresets.Any() && !audioPresets.Any() && !imagePresets.Any()) return;

        bool isLightTheme = IsWindowsLightTheme();
        string exeDir = Path.GetDirectoryName(exePath) ?? "";
        string iconsDir = Path.Combine(exeDir, "Icons");
        string customIconsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RenderPard", "CustomIcons");

        string rootIcon = ResolveThemeIconPath("renderpard", iconsDir, customIconsDir, isLightTheme);

        string basePath = $@"Software\Classes\Directory\shell\{MenuName}";

        using (RegistryKey baseKey = Registry.CurrentUser.CreateSubKey(basePath))
        {
            if (baseKey == null) return;
            baseKey.SetValue("MUIVerb", "RenderPard");
            baseKey.SetValue("Icon", !string.IsNullOrEmpty(rootIcon) && File.Exists(rootIcon) ? $"\"{rootIcon}\"" : $"\"{exePath}\"");
            baseKey.SetValue("SubCommands", "");
        }

        if (videoPresets.Any()) RegisterDirectorySubMenu("Video", "RenderPard 🎬 (Видео)", videoPresets, exePath, isLightTheme, iconsDir, customIconsDir, rootIcon);
        if (audioPresets.Any()) RegisterDirectorySubMenu("Audio", "RenderPard 🎵 (Аудио)", audioPresets, exePath, isLightTheme, iconsDir, customIconsDir, rootIcon);
        if (imagePresets.Any()) RegisterDirectorySubMenu("Image", "RenderPard 🖼 (Изображения)", imagePresets, exePath, isLightTheme, iconsDir, customIconsDir, rootIcon);
    }

    private static void RegisterDirectorySubMenu(string category, string displayTitle, List<Preset> presets, string exePath, bool isLightTheme, string iconsDir, string customIconsDir, string rootIcon)
    {
        string shellPath = $@"Software\Classes\Directory\shell\{MenuName}\shell\{category}";
        using (RegistryKey categoryKey = Registry.CurrentUser.CreateSubKey(shellPath))
        {
            if (categoryKey == null) return;
            categoryKey.SetValue("MUIVerb", displayTitle);
            
            string iconName = category == "Video" ? "video" : (category == "Audio" ? "audio" : "image");
            string categoryIcon = ResolveThemeIconPath(iconName, iconsDir, customIconsDir, isLightTheme);
            if (!string.IsNullOrEmpty(categoryIcon) && File.Exists(categoryIcon))
                categoryKey.SetValue("Icon", $"\"{categoryIcon}\"");
            else if (!string.IsNullOrEmpty(rootIcon) && File.Exists(rootIcon))
                categoryKey.SetValue("Icon", $"\"{rootIcon}\"");
                
            categoryKey.SetValue("SubCommands", "");
        }

        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            string safeName = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars()));
            string presetKeyPath = $@"{shellPath}\shell\{i:D3}_{safeName}";
            
            using (RegistryKey presetKey = Registry.CurrentUser.CreateSubKey(presetKeyPath))
            {
                if (presetKey == null) continue;
                presetKey.SetValue("MUIVerb", preset.Name);
                
                string presetIconPath = ResolveThemeIconPath(preset.CustomIcon, iconsDir, customIconsDir, isLightTheme);
                if (string.IsNullOrEmpty(presetIconPath) || !File.Exists(presetIconPath))
                {
                    string typeKey = preset.IsAudioPreset ? "audio" : (preset.IsImagePreset ? "image" : "renderpard");
                    string typeFallback = ResolveThemeIconPath(typeKey, iconsDir, customIconsDir, isLightTheme);
                    presetIconPath = (!string.IsNullOrEmpty(typeFallback) && File.Exists(typeFallback)) ? typeFallback : (!string.IsNullOrEmpty(rootIcon) && File.Exists(rootIcon) ? rootIcon : exePath);
                }
                
                presetKey.SetValue("Icon", $"\"{presetIconPath}\"");
                
                using (RegistryKey commandKey = presetKey.CreateSubKey("command"))
                {
                    if (commandKey != null)
                        commandKey.SetValue("", $"\"{exePath}\" --preset \"{preset.Name}\" --file \"%1\"");
                }
            }
        }
    }

    private static void RegisterSubMenu(string ext, string keyName, string displayTitle, List<Preset> presets, string exePath)
    {
        try
        {
            bool isLightTheme = IsWindowsLightTheme();
            string exeDir = Path.GetDirectoryName(exePath) ?? "";
            string iconsDir = Path.Combine(exeDir, "Icons");
            string customIconsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RenderPard", "CustomIcons");

            string rootIcon = ResolveThemeIconPath("renderpard", iconsDir, customIconsDir, isLightTheme);
            string settingsIcon = ResolveThemeIconPath("settings", iconsDir, customIconsDir, isLightTheme);
            string cutIcon = ResolveThemeIconPath("cut", iconsDir, customIconsDir, isLightTheme);

            string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{keyName}";

            using (RegistryKey baseKey = Registry.CurrentUser.CreateSubKey(basePath))
            {
                if (baseKey == null) return;

                baseKey.SetValue("MUIVerb", displayTitle);
                baseKey.SetValue("Icon", !string.IsNullOrEmpty(rootIcon) && File.Exists(rootIcon) ? $"\"{rootIcon}\"" : $"\"{exePath}\"");
                
                baseKey.SetValue("SubCommands", "");
                try { baseKey.DeleteValue("ExtendedSubCommandsKey"); } catch { }
                baseKey.SetValue("MultiSelectModel", "Player");
            }

            string subCommandsPath = basePath + @"\shell";
            
            try { Registry.CurrentUser.DeleteSubKeyTree(subCommandsPath, false); } catch { }

            using (RegistryKey shellKey = Registry.CurrentUser.CreateSubKey(subCommandsPath))
            {
                if (shellKey == null) return;

                // Add "Settings" option at the very top
                using (RegistryKey settingsKey = shellKey.CreateSubKey("000_Settings"))
                {
                    if (settingsKey != null)
                    {
                        settingsKey.SetValue("MUIVerb", "Настройки");
                        settingsKey.SetValue("Icon", !string.IsNullOrEmpty(settingsIcon) && File.Exists(settingsIcon) ? $"\"{settingsIcon}\"" : $"\"{exePath}\"");
                        using (RegistryKey commandKey = settingsKey.CreateSubKey("command"))
                        {
                            if (commandKey != null)
                            {
                                commandKey.SetValue("", $"\"{exePath}\" --open");
                            }
                        }
                    }
                }

                // Add "Trim" option for video & audio
                if (VideoExtensions.Contains(ext) || AudioExtensions.Contains(ext))
                {
                    using (RegistryKey trimKey = shellKey.CreateSubKey("001_Trim"))
                    {
                        if (trimKey != null)
                        {
                            trimKey.SetValue("MUIVerb", "Обрезать (In / Out)...");
                            trimKey.SetValue("Icon", !string.IsNullOrEmpty(cutIcon) && File.Exists(cutIcon) ? $"\"{cutIcon}\"" : $"\"{exePath}\"");
                            using (RegistryKey commandKey = trimKey.CreateSubKey("command"))
                            {
                                if (commandKey != null)
                                {
                                    commandKey.SetValue("", $"\"{exePath}\" --trim \"%1\"");
                                }
                            }
                        }
                    }
                }

                for (int i = 0; i < presets.Count; i++)
                {
                    var preset = presets[i];
                    if (!preset.ShowInContextMenu) continue;

                    string safeName = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars()));
                    string presetKeyPath = $@"{subCommandsPath}\{i + 2:D3}_{safeName}";
                    
                    using (RegistryKey presetKey = Registry.CurrentUser.CreateSubKey(presetKeyPath))
                    {
                        if (presetKey == null) continue;

                        presetKey.SetValue("MUIVerb", preset.Name);
                        
                        string presetIconPath = ResolveThemeIconPath(preset.CustomIcon, iconsDir, customIconsDir, isLightTheme);

                        if (string.IsNullOrEmpty(presetIconPath) || !File.Exists(presetIconPath))
                        {
                            // Type-specific clean fallback icon with theme awareness
                            string typeKey = preset.IsAudioPreset ? "audio" : (preset.IsImagePreset ? "image" : "renderpard");
                            string typeFallback = ResolveThemeIconPath(typeKey, iconsDir, customIconsDir, isLightTheme);

                            if (!string.IsNullOrEmpty(typeFallback) && File.Exists(typeFallback))
                            {
                                presetIconPath = typeFallback;
                            }
                            else if (!string.IsNullOrEmpty(rootIcon) && File.Exists(rootIcon))
                            {
                                presetIconPath = rootIcon;
                            }
                            else
                            {
                                presetIconPath = exePath;
                            }
                        }
                        
                        presetKey.SetValue("Icon", $"\"{presetIconPath}\"");
                        
                        using (RegistryKey commandKey = presetKey.CreateSubKey("command"))
                        {
                            if (commandKey != null)
                            {
                                commandKey.SetValue("", $"\"{exePath}\" --preset \"{preset.Name}\" --file \"%1\"");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register context menu for {ext}: {ex.Message}");
        }
    }

    private static void UnregisterForExtension(string ext)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}", false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Video", false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Audio", false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Image", false); } catch { }
    }
}
