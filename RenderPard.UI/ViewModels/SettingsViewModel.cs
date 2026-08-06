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
    private Preset _selectedPreset;
    private bool _openFolderOnCompletion;
    private bool _minimizeToTrayOnClose;
    private string _language;
        
    public ObservableCollection<Preset> Presets { get; }

    public Preset SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public bool OpenFolderOnCompletion
    {
        get => _openFolderOnCompletion;
        set { if (SetProperty(ref _openFolderOnCompletion, value)) SaveGlobalSettings(); }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set { if (SetProperty(ref _minimizeToTrayOnClose, value)) SaveGlobalSettings(); }
    }

    public string Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                SaveGlobalSettings();
                UpdateLanguageDictionary();
            }
        }
    }

    [RelayCommand]
    private void ChangeLanguage(string lang)
    {
        Language = lang;
    }

    [RelayCommand]
    private void OpenGlobalSettings()
    {
        var globalSettingsWindow = new GlobalSettingsWindow();
        globalSettingsWindow.ShowDialog();
    }

    private void UpdateLanguageDictionary()
    {
        string langFile = _language == "en-US" ? "Lang.en-US.xaml" : "Lang.ru-RU.xaml";
        var uri = new Uri($"/RenderPard.UI;component/Themes/{langFile}", UriKind.RelativeOrAbsolute);
        
        var appResources = System.Windows.Application.Current.Resources;
        var oldDict = appResources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Lang."));
        if (oldDict != null)
        {
            appResources.MergedDictionaries.Remove(oldDict);
        }
        appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = uri });
    }

    private void SaveGlobalSettings()
    {
        App.Settings.OpenFolderOnCompletion = _openFolderOnCompletion;
        App.Settings.MinimizeToTrayOnClose = _minimizeToTrayOnClose;
        App.Settings.Language = _language;
        AppSettingsManager.SaveSettings(App.Settings);
    }

    public SettingsViewModel()
    {
        _openFolderOnCompletion = App.Settings.OpenFolderOnCompletion;
        _minimizeToTrayOnClose = App.Settings.MinimizeToTrayOnClose;
        _language = App.Settings.Language;

        var loadedPresets = App.PresetManager.LoadPresets();
        Presets = new ObservableCollection<Preset>(loadedPresets);
        if (Presets.Any())
        {
            SelectedPreset = Presets.First();
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
