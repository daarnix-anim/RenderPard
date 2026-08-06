using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using RenderPard.Core.Models;

namespace RenderPard.Core;

[SupportedOSPlatform("windows")]
public static class ContextMenuManager
{
    private static readonly string[] SupportedExtensions = { ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm" };
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
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\SystemFileAssociations\.mp4\shell\{MenuName}");
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterForExtension(string ext, List<Preset> presets, string exePath)
    {
        try
        {
            string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}";

            using (RegistryKey baseKey = Registry.CurrentUser.CreateSubKey(basePath))
            {
                if (baseKey == null) return;

                baseKey.SetValue("MUIVerb", "RenderPard");
                baseKey.SetValue("Icon", $"\"{exePath}\"");
                
                // Use SubCommands empty string for static cascading menus
                baseKey.SetValue("SubCommands", "");
                
                // Remove ExtendedSubCommandsKey if it exists from previous buggy version
                try { baseKey.DeleteValue("ExtendedSubCommandsKey"); } catch { }

                baseKey.SetValue("MultiSelectModel", "Player"); // Allows multiple files selection
            }

            string subCommandsPath = basePath + @"\shell";
            
            // Clean up existing presets in registry first to avoid orphans
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(subCommandsPath, false);
            }
            catch { }

            using (RegistryKey shellKey = Registry.CurrentUser.CreateSubKey(subCommandsPath))
            {
                if (shellKey == null) return;

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
                        string presetIconPath = Path.Combine(exeDir, $"icon_{safeName}.ico");
                        
                        if (File.Exists(presetIconPath))
                        {
                            presetKey.SetValue("Icon", $"\"{presetIconPath}\"");
                        }
                        else
                        {
                            presetKey.SetValue("Icon", $"\"{exePath}\"");
                        }
                        
                        using (RegistryKey commandKey = presetKey.CreateSubKey("command"))
                        {
                            if (commandKey != null)
                            {
                                // Pass the preset name safely and use %1 for the file path
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
        try
        {
            string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuName}";
            Registry.CurrentUser.DeleteSubKeyTree(basePath, false);
        }
        catch { }
    }
}
