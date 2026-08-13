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
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm" };
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".ai", ".pdf" };
    private static readonly string[] SupportedExtensions = VideoExtensions.Concat(ImageExtensions).ToArray();
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
            // Check either of the new keys
            using var key1 = Registry.CurrentUser.OpenSubKey($@"Software\Classes\SystemFileAssociations\.mp4\shell\{MenuName}_Video");
            using var key2 = Registry.CurrentUser.OpenSubKey($@"Software\Classes\SystemFileAssociations\.jpg\shell\{MenuName}_Image");
            return key1 != null || key2 != null;
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
        bool isImage = ImageExtensions.Contains(ext);

        var videoPresets = presets.Where(p => !p.IsImagePreset).ToList();
        var imagePresets = presets.Where(p => p.IsImagePreset).ToList();

        if (isVideo && videoPresets.Any())
            RegisterSubMenu(ext, $"{MenuName}_Video", "RenderPard 🎬", videoPresets, exePath);
        else
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Video", false); } catch { }

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
                        string presetIconPath;
                        string iconsDir = Path.Combine(exeDir, "Icons");
                        string possibleIcon = Path.Combine(iconsDir, preset.CustomIcon + ".ico");
                        
                        if (Path.IsPathRooted(preset.CustomIcon) && File.Exists(preset.CustomIcon))
                        {
                            presetIconPath = preset.CustomIcon;
                        }
                        else if (File.Exists(possibleIcon))
                        {
                            presetIconPath = possibleIcon;
                        }
                        else
                        {
                            string iconIdentifier = string.IsNullOrEmpty(preset.CustomIcon) || preset.CustomIcon == "Default" 
                                ? "none" 
                                : string.Join("_", preset.CustomIcon.Split(Path.GetInvalidFileNameChars()));
                            presetIconPath = Path.Combine(exeDir, $"icon_{iconIdentifier}.ico");
                        }
                        
                        if (File.Exists(presetIconPath))
                            presetKey.SetValue("Icon", $"\"{presetIconPath}\"");
                        else
                            presetKey.SetValue("Icon", $"\"{exePath}\"");
                        
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
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}_Image", false); } catch { }
    }
}
