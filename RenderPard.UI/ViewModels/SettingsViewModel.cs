using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenderPard.Core;
using RenderPard.Core.Models;

namespace RenderPard.UI.ViewModels;

public class IconItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public System.Windows.Media.ImageSource? Image { get; set; }
}

public partial class SettingsViewModel : ObservableObject
{
    private Preset _selectedPreset;
    private bool _openFolderOnCompletion;
    private bool _minimizeToTrayOnClose;
    private string _language;
        
    public ObservableCollection<Preset> Presets { get; }
    public System.Collections.Generic.IEnumerable<Preset> VideoPresets => Presets.Where(p => !p.IsImagePreset);
    public System.Collections.Generic.IEnumerable<Preset> ImagePresets => Presets.Where(p => p.IsImagePreset);
    
    public ObservableCollection<IconItemViewModel> AvailableIcons { get; } = new();

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
        Presets.CollectionChanged += (s, e) => 
        {
            OnPropertyChanged(nameof(VideoPresets));
            OnPropertyChanged(nameof(ImagePresets));
        };
        
        // Load default ICOs from Icons
        string iconsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Icons");
        
        // Always load built-in icons from generator
        foreach (var iconName in IconGenerator.AvailableIcons)
        {
            var image = IconGenerator.GetIconImageSource(iconName);
            AvailableIcons.Add(new IconItemViewModel { Name = iconName, Image = image });
        }

        // Add any external icons found in the Icons directory
        if (System.IO.Directory.Exists(iconsDir))
        {
            foreach (var icoFile in System.IO.Directory.GetFiles(iconsDir, "*.ico"))
            {
                string iconName = System.IO.Path.GetFileNameWithoutExtension(icoFile);
                if (!AvailableIcons.Any(i => i.Name == iconName))
                {
                    AvailableIcons.Add(new IconItemViewModel 
                    { 
                        Name = iconName, 
                        Image = TryLoadExternalIcon(icoFile)
                    });
                }
            }
        }

        foreach (var preset in Presets)
        {
            if (!string.IsNullOrEmpty(preset.CustomIcon) && !AvailableIcons.Any(i => i.Name == preset.CustomIcon))
            {
                AvailableIcons.Add(new IconItemViewModel { Name = preset.CustomIcon, Image = TryLoadExternalIcon(preset.CustomIcon) });
            }
        }

        if (Presets.Any())
        {
            SelectedPreset = Presets.First();
        }
    }

    private System.Windows.Media.ImageSource? TryLoadExternalIcon(string path)
    {
        try
        {
            if (System.IO.Path.GetExtension(path).ToLower() == ".ico")
            {
                return new System.Windows.Media.Imaging.BitmapImage(new System.Uri(path));
            }
        }
        catch { }
        return null;
    }

    [RelayCommand]
    private void BrowseCustomIcon()
    {
        if (SelectedPreset == null) return;
        
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Icon files (*.ico)|*.ico|All files (*.*)|*.*",
            Title = "Select Custom Icon"
        };
        
        if (dialog.ShowDialog() == true)
        {
            if (!AvailableIcons.Any(i => i.Name == dialog.FileName))
            {
                AvailableIcons.Add(new IconItemViewModel { Name = dialog.FileName, Image = TryLoadExternalIcon(dialog.FileName) });
            }
            
            SelectedPreset.CustomIcon = dialog.FileName;
            // Force UI update
            var temp = SelectedPreset;
            SelectedPreset = null!;
            SelectedPreset = temp;
        }
    }

    [RelayCommand]
    private void PickIcon()
    {
        if (SelectedPreset == null) return;
        
        var dialog = new IconPickerDialog(AvailableIcons, SelectedPreset.CustomIcon);
        dialog.Owner = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault() ?? System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            SelectedPreset.CustomIcon = dialog.SelectedIconName;
            // Force UI update
            var temp = SelectedPreset;
            SelectedPreset = null!;
            SelectedPreset = temp;
        }
    }

    [RelayCommand]
    private void AddVideoPreset()
    {
        var newPreset = new Preset
        {
            Name = "New Video Preset",
            ShowInContextMenu = true,
            Container = ContainerFormat.Mp4,
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
    private void AddImagePreset()
    {
        var newPreset = new Preset
        {
            Name = "New Image Preset",
            ShowInContextMenu = true,
            Container = ContainerFormat.Jpeg,
            ImageQuality = 80,
            FilenamePattern = "{original}_{preset}"
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
        
        // Generate icons if they don't exist
        string currentDir = System.AppDomain.CurrentDomain.BaseDirectory;
        IconGenerator.EnsureIconsExist(Presets.Select(p => p.CustomIcon), currentDir);

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
