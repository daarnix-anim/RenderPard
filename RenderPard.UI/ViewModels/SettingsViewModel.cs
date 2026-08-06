using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenderPard.Core;
using RenderPard.Core.Models;

namespace RenderPard.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Preset> _presets;

    [ObservableProperty]
    private Preset _selectedPreset;

    public SettingsViewModel()
    {
        var loadedPresets = App.PresetManager.LoadPresets();
        _presets = new ObservableCollection<Preset>(loadedPresets);
        if (_presets.Any())
        {
            _selectedPreset = _presets.First();
        }
    }

    [RelayCommand]
    private void AddPreset()
    {
        var newPreset = new Preset
        {
            Name = "New Preset",
            ShowInContextMenu = true,
            VideoCodec = VideoCodec.H264_Nvenc,
            TargetVideoBitrateKbps = 2000,
            AudioMode = AudioMode.Encode,
            AudioCodec = AudioCodec.Aac,
            AudioBitrateKbps = 128
        };
        Presets.Add(newPreset);
        SelectedPreset = newPreset;
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset != null)
        {
            Presets.Remove(SelectedPreset);
            SelectedPreset = Presets.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void Save(System.Windows.Window window)
    {
        // Sync timecode styles
        foreach (var preset in Presets)
        {
            if (preset.TimecodeStyles.Count > 0)
            {
                var master = preset.TimecodeStyles[0];
                for (int i = 1; i < preset.TimecodeStyles.Count; i++)
                {
                    preset.TimecodeStyles[i].FontSize = master.FontSize;
                    preset.TimecodeStyles[i].Color = master.Color;
                    preset.TimecodeStyles[i].Opacity = master.Opacity;
                    preset.TimecodeStyles[i].Position = master.Position;
                }
            }
        }

        App.PresetManager.SavePresets(Presets.ToList());
        // Auto-register context menu to reflect changes
        ContextMenuManager.Register(Presets.ToList());
        window?.Close();
    }

    [RelayCommand]
    private void Cancel(System.Windows.Window window)
    {
        window?.Close();
    }
}
