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
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm", ".wmv", ".flv", ".ts", ".mts", ".m2ts", ".3gp" };
    private static readonly string[] AudioExtensions = { ".wav", ".wave", ".ogg", ".opus", ".m4a", ".aac", ".flac", ".aif", ".aiff", ".aifc", ".amr", ".3ga", ".caf", ".wma", ".weba", ".mp2", ".ac3", ".alac", ".ape", ".wv", ".mp3" };
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".ai", ".pdf", ".heic", ".cr2", ".nef", ".arw", ".dng" };
    private static readonly string[] SupportedExtensions = VideoExtensions.Concat(AudioExtensions).Concat(ImageExtensions).Distinct().ToArray();
    private const string MenuName = "RenderPard";

    public static void Register(List<Preset> presets)
    {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(exePath)) return;

        foreach (var ext in SupportedExtensions)
        {
            RegisterForExtension(ext, presets, exePath);
        }
    }

    public static void Unregister()
    {
        foreach (var ext in SupportedExtensions)
        {
            UnregisterForExtension(ext);
        }
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

    private static void RegisterSubMenu(string ext, string keyName, string displayTitle, List<Preset> presets, string exePath)
    {
        try
        {
            string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{keyName}";

            using (RegistryKey baseKey = Registry.CurrentUser.CreateSubKey(basePath))
            {
                if (baseKey == null) return;

                baseKey.SetValue("MUIVerb", displayTitle);
                baseKey.SetValue("Icon", $"\"{exePath}\"");
                
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
                        settingsKey.SetValue("Icon", $"\"{exePath}\"");
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
                            trimKey.SetValue("MUIVerb", "✂ Обрезать (In / Out)...");
                            trimKey.SetValue("Icon", $"\"{exePath}\"");
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
                    string presetKeyPath = $@"{subCommandsPath}\{i:D2}_{safeName}";
                    
                    using (RegistryKey presetKey = Registry.CurrentUser.CreateSubKey(presetKeyPath))
                    {
                        if (presetKey == null) continue;

                        presetKey.SetValue("MUIVerb", preset.Name);
                        
                        string exeDir = Path.GetDirectoryName(exePath) ?? "";
                        string presetIconPath = string.Empty;
                        string iconsDir = Path.Combine(exeDir, "Icons");
                        string customIconsDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "RenderPard", "CustomIcons");
                        
                        if (!string.IsNullOrEmpty(preset.CustomIcon))
                        {
                            string possibleBuiltin = Path.Combine(iconsDir, preset.CustomIcon + ".ico");
                            string possibleCustom = Path.Combine(customIconsDir, preset.CustomIcon + ".ico");
                            string possibleCustomDirect = Path.Combine(customIconsDir, preset.CustomIcon);

                            if (Path.IsPathRooted(preset.CustomIcon) && File.Exists(preset.CustomIcon))
                            {
                                presetIconPath = preset.CustomIcon;
                            }
                            else if (File.Exists(possibleBuiltin))
                            {
                                presetIconPath = possibleBuiltin;
                            }
                            else if (File.Exists(possibleCustom))
                            {
                                presetIconPath = possibleCustom;
                            }
                            else if (File.Exists(possibleCustomDirect))
                            {
                                presetIconPath = possibleCustomDirect;
                            }
                        }

                        if (string.IsNullOrEmpty(presetIconPath) || !File.Exists(presetIconPath))
                        {
                            // Type-specific clean fallback icon
                            string typeFallback = preset.IsAudioPreset
                                ? Path.Combine(iconsDir, "audio.ico")
                                : (preset.IsImagePreset ? Path.Combine(iconsDir, "image.ico") : Path.Combine(iconsDir, "video.ico"));

                            if (File.Exists(typeFallback))
                            {
                                presetIconPath = typeFallback;
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
