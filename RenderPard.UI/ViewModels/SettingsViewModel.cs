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
        
    private int _selectedTabIndex = 0; // 0 = Video, 1 = Photo, 2 = Audio
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsVideoTabSelected));
                OnPropertyChanged(nameof(IsPhotoTabSelected));
                OnPropertyChanged(nameof(IsAudioTabSelected));
                FilteredPresetsView.Refresh();
                SelectedPreset = FilteredPresetsView.Cast<Preset>().FirstOrDefault();
            }
        }
    }

    public bool IsVideoTabSelected
    {
        get => _selectedTabIndex == 0;
        set { if (value) SelectedTabIndex = 0; }
    }

    public bool IsPhotoTabSelected
    {
        get => _selectedTabIndex == 1;
        set { if (value) SelectedTabIndex = 1; }
    }

    public bool IsAudioTabSelected
    {
        get => _selectedTabIndex == 2;
        set { if (value) SelectedTabIndex = 2; }
    }

    public System.ComponentModel.ICollectionView FilteredPresetsView { get; }

    public ObservableCollection<Preset> Presets { get; }
    public System.Collections.Generic.IEnumerable<Preset> VideoPresets => Presets.Where(p => p.IsVideoPreset);
    public System.Collections.Generic.IEnumerable<Preset> ImagePresets => Presets.Where(p => p.IsImagePreset);
    public System.Collections.Generic.IEnumerable<Preset> AudioPresets => Presets.Where(p => p.IsAudioPreset);
    
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
        FilteredPresetsView = System.Windows.Data.CollectionViewSource.GetDefaultView(Presets);
        FilteredPresetsView.Filter = item => 
        {
            if (item is Preset p)
            {
                if (IsVideoTabSelected) return p.IsVideoPreset;
                if (IsPhotoTabSelected) return p.IsImagePreset;
                if (IsAudioTabSelected) return p.IsAudioPreset;
            }
            return false;
        };
        Presets.CollectionChanged += (s, e) => 
        {
            OnPropertyChanged(nameof(VideoPresets));
            OnPropertyChanged(nameof(ImagePresets));
            OnPropertyChanged(nameof(AudioPresets));
        };
        
        string iconsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Icons");
        string customIconsDir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), 
            "RenderPard", "CustomIcons");

        try
        {
            if (!System.IO.Directory.Exists(customIconsDir))
            {
                System.IO.Directory.CreateDirectory(customIconsDir);
            }
        }
        catch { }

        // 1. Load built-in ICO files from Icons directory (86 icons)
        if (System.IO.Directory.Exists(iconsDir))
        {
            var icoFiles = System.IO.Directory.GetFiles(iconsDir, "*.ico")
                .OrderBy(f => System.IO.Path.GetFileName(f));

            foreach (var icoFile in icoFiles)
            {
                string iconName = System.IO.Path.GetFileNameWithoutExtension(icoFile);
                if (!AvailableIcons.Any(i => i.Name == iconName))
                {
                    var img = TryLoadExternalIcon(icoFile);
                    if (img != null)
                    {
                        AvailableIcons.Add(new IconItemViewModel { Name = iconName, Image = img });
                    }
                }
            }
        }

        // 2. Load custom persistent user icons from %AppData%\RenderPard\CustomIcons
        if (System.IO.Directory.Exists(customIconsDir))
        {
            foreach (var customFile in System.IO.Directory.GetFiles(customIconsDir, "*.*"))
            {
                string iconName = System.IO.Path.GetFileNameWithoutExtension(customFile);
                if (!AvailableIcons.Any(i => i.Name == iconName || i.Name == customFile))
                {
                    var img = TryLoadExternalIcon(customFile);
                    if (img != null)
                    {
                        AvailableIcons.Add(new IconItemViewModel { Name = customFile, Image = img });
                    }
                }
            }
        }

        // 3. For any preset with custom file path, load it as well
        foreach (var preset in Presets)
        {
            if (!string.IsNullOrEmpty(preset.CustomIcon) && !AvailableIcons.Any(i => i.Name == preset.CustomIcon))
            {
                var img = TryLoadExternalIcon(preset.CustomIcon);
                if (img != null)
                {
                    AvailableIcons.Add(new IconItemViewModel { Name = preset.CustomIcon, Image = img });
                }
            }
        }

        if (Presets.Any())
        {
            SelectedPreset = FilteredPresetsView.Cast<Preset>().FirstOrDefault() ?? Presets.First();
        }
    }

    private System.Windows.Media.ImageSource? TryLoadExternalIcon(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new System.Uri(path, System.UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
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
            Filter = "Icon files (*.ico;*.png;*.jpg;*.jpeg;*.webp)|*.ico;*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
            Title = "Выберите иконку"
        };
        
        if (dialog.ShowDialog() == true)
        {
            string finalPath = dialog.FileName;
            try
            {
                string customIconsDir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "RenderPard", "CustomIcons");
                if (!System.IO.Directory.Exists(customIconsDir))
                {
                    System.IO.Directory.CreateDirectory(customIconsDir);
                }

                string fileName = System.IO.Path.GetFileName(dialog.FileName);
                string destPath = System.IO.Path.Combine(customIconsDir, fileName);
                System.IO.File.Copy(dialog.FileName, destPath, true);
                finalPath = destPath;
            }
            catch { }

            if (!AvailableIcons.Any(i => i.Name == finalPath))
            {
                AvailableIcons.Add(new IconItemViewModel { Name = finalPath, Image = TryLoadExternalIcon(finalPath) });
            }
            
            SelectedPreset.CustomIcon = finalPath;
            // Force UI update
            var temp = SelectedPreset;
            SelectedPreset = null!;
            SelectedPreset = temp;

            AutoRefreshContextMenu();
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
            if (!AvailableIcons.Any(i => i.Name == dialog.SelectedIconName))
            {
                AvailableIcons.Add(new IconItemViewModel { Name = dialog.SelectedIconName, Image = TryLoadExternalIcon(dialog.SelectedIconName) });
            }

            // Force UI update
            var temp = SelectedPreset;
            SelectedPreset = null!;
            SelectedPreset = temp;

            AutoRefreshContextMenu();
        }
    }

    private void AutoRefreshContextMenu()
    {
        try
        {
            if (ContextMenuManager.IsRegistered())
            {
                App.PresetManager.SavePresets(Presets.ToList());
                ContextMenuManager.Register(Presets.ToList());
            }
        }
        catch { }
    }

    public void MovePreset(Preset source, Preset target, bool insertAfter = false)
    {
        if (source == null || target == null || ReferenceEquals(source, target))
            return;

        int sourceIndex = Presets.IndexOf(source);
        int targetIndex = Presets.IndexOf(target);

        if (sourceIndex < 0 || targetIndex < 0)
            return;

        int destinationIndex;
        if (insertAfter)
        {
            destinationIndex = (sourceIndex < targetIndex) ? targetIndex : targetIndex + 1;
        }
        else
        {
            destinationIndex = (sourceIndex < targetIndex) ? targetIndex - 1 : targetIndex;
        }

        if (destinationIndex < 0) destinationIndex = 0;
        if (destinationIndex >= Presets.Count) destinationIndex = Presets.Count - 1;

        if (sourceIndex != destinationIndex)
        {
            Presets.Move(sourceIndex, destinationIndex);

            for (int i = 0; i < Presets.Count; i++)
            {
                Presets[i].SortOrder = i;
            }

            FilteredPresetsView.Refresh();
            SelectedPreset = source;

            App.PresetManager.SavePresets(Presets.ToList());
            AutoRefreshContextMenu();
        }
    }

    [RelayCommand]
    private void AddPreset()
    {
        if (IsVideoTabSelected)
        {
            var newPreset = new Preset
            {
                Name = "New Video Preset",
                ShowInContextMenu = true,
                SortOrder = Presets.Count,
                Container = ContainerFormat.Mp4,
                VideoCodec = VideoCodec.H264_Nvenc,
                TargetVideoBitrateKbps = 2000,
                AudioMode = AudioMode.Encode,
                AudioCodec = AudioCodec.Aac,
                AudioBitrateKbps = 192,
                CustomIcon = "video"
            };
            Presets.Add(newPreset);
            FilteredPresetsView.Refresh();
            SelectedPreset = newPreset;
        }
        else if (IsPhotoTabSelected)
        {
            var newPreset = new Preset
            {
                Name = "New Image Preset",
                ShowInContextMenu = true,
                SortOrder = Presets.Count,
                Container = ContainerFormat.Jpeg,
                ImageQuality = 80,
                FilenamePattern = "{original}_{preset}",
                CustomIcon = "image"
            };
            Presets.Add(newPreset);
            FilteredPresetsView.Refresh();
            SelectedPreset = newPreset;
        }
        else if (IsAudioTabSelected)
        {
            var newPreset = new Preset
            {
                Name = "New Audio Preset",
                ShowInContextMenu = true,
                SortOrder = Presets.Count,
                Container = ContainerFormat.Mp3,
                AudioCodec = AudioCodec.Mp3,
                AudioBitrateKbps = 192,
                AudioSampleRate = AudioSampleRate.Hz48000,
                AudioChannels = AudioChannels.Stereo,
                CustomIcon = "fmt_mp3"
            };
            Presets.Add(newPreset);
            FilteredPresetsView.Refresh();
            SelectedPreset = newPreset;
        }
        App.PresetManager.SavePresets(Presets.ToList());
        AutoRefreshContextMenu();
    }

    [RelayCommand]
    private void AddVideoPreset()
    {
        var newPreset = new Preset
        {
            Name = "New Video Preset",
            ShowInContextMenu = true,
            SortOrder = Presets.Count,
            Container = ContainerFormat.Mp4,
            VideoCodec = VideoCodec.H264_Nvenc,
            TargetVideoBitrateKbps = 2000,
            AudioMode = AudioMode.Encode,
            AudioCodec = AudioCodec.Aac,
            AudioBitrateKbps = 128
        };
        Presets.Add(newPreset);
        FilteredPresetsView.Refresh();
        SelectedPreset = newPreset;
        App.PresetManager.SavePresets(Presets.ToList());
        AutoRefreshContextMenu();
    }

    [RelayCommand]
    private void AddImagePreset()
    {
        var newPreset = new Preset
        {
            Name = "New Image Preset",
            ShowInContextMenu = true,
            SortOrder = Presets.Count,
            Container = ContainerFormat.Jpeg,
            ImageQuality = 80,
            FilenamePattern = "{original}_{preset}"
        };
        Presets.Add(newPreset);
        FilteredPresetsView.Refresh();
        SelectedPreset = newPreset;
        App.PresetManager.SavePresets(Presets.ToList());
        AutoRefreshContextMenu();
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset != null)
        {
            Presets.Remove(SelectedPreset);
            for (int i = 0; i < Presets.Count; i++)
            {
                Presets[i].SortOrder = i;
            }
            FilteredPresetsView.Refresh();
            SelectedPreset = FilteredPresetsView.Cast<Preset>().FirstOrDefault();
            App.PresetManager.SavePresets(Presets.ToList());
            AutoRefreshContextMenu();
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

        // Auto-register and refresh context menu
        ContextMenuManager.RefreshIfRegistered(Presets.ToList());
        window?.Close();
    }

    [RelayCommand]
    private void Cancel(System.Windows.Window window)
    {
        window?.Close();
    }
}
