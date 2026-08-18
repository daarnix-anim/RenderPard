using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RenderPard.Core.Models;
using RenderPard.UI.ViewModels;

namespace RenderPard.UI;

public partial class MainWindow : Window
{
    private QueueViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        if (App.Settings.WindowWidth > 0 && App.Settings.WindowHeight > 0)
        {
            this.Width = App.Settings.WindowWidth;
            this.Height = App.Settings.WindowHeight;
        }

        _viewModel = new QueueViewModel();
        DataContext = _viewModel;
        
        this.Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // GitHub Updater check. 
        string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.4.0";
        _ = GitHubUpdater.CheckForUpdatesAsync("daarnix-anim", "RenderPard", currentVersion);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Minimized)
        {
            this.ShowInTaskbar = false;
            TrayIcon.Visibility = Visibility.Visible;
        }
        else
        {
            this.ShowInTaskbar = true;
            TrayIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (this.WindowState == WindowState.Normal)
        {
            App.Settings.WindowWidth = this.ActualWidth;
            App.Settings.WindowHeight = this.ActualHeight;
            AppSettingsManager.SaveSettings(App.Settings);
        }

        if (App.Settings.MinimizeToTrayOnClose && this.WindowState != WindowState.Minimized)
        {
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
        }
        else
        {
            TrayIcon.Dispose();
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreWindow();
    }

    private void MenuItemRestore_Click(object sender, RoutedEventArgs e)
    {
        RestoreWindow();
    }

    private void MenuItemExit_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.MinimizeToTrayOnClose = false; // Override to allow exit
        this.Close();
    }

    private void RestoreWindow()
    {
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    public void EnqueueFile(string filePath, Preset preset)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _viewModel.EnqueueFile(filePath, preset);
        });
    }

    public void OpenTrimWindow(string filePath, Preset? preset = null)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _viewModel.OpenTrimWindowForFile(filePath, preset);
        });
    }

    public void StartQueue()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _viewModel.IsQueueActive = true;
        });
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            var presets = App.PresetManager.LoadPresets();
            if (presets == null || presets.Count == 0)
            {
                MessageBox.Show("У вас нет созданных пресетов! Сначала создайте пресет в настройках.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var contextMenu = new System.Windows.Controls.ContextMenu();
            var iconConverter = new IconNameToImageSourceConverter();
            
            var headerItem = new System.Windows.Controls.MenuItem 
            { 
                Header = "Выберите действие / пресет:", 
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };
            contextMenu.Items.Add(headerItem);

            if (files.Length == 1)
            {
                var trimItem = new System.Windows.Controls.MenuItem 
                { 
                    Header = "Обрезать фрагмент (In / Out)...",
                    FontWeight = FontWeights.SemiBold
                };

                var trimIcon = iconConverter.Convert("cut", typeof(System.Windows.Media.ImageSource), string.Empty, System.Globalization.CultureInfo.CurrentCulture) as System.Windows.Media.ImageSource;
                if (trimIcon != null)
                {
                    trimItem.Icon = new System.Windows.Controls.Image
                    {
                        Source = trimIcon,
                        Width = 16,
                        Height = 16
                    };
                }
                else
                {
                    trimItem.Icon = new System.Windows.Controls.TextBlock
                    {
                        Text = "✂",
                        Foreground = (System.Windows.Media.Brush)FindResource("AppLinkBrush"),
                        FontSize = 13,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                trimItem.Click += (s, args) =>
                {
                    _viewModel.OpenTrimWindowForFile(files[0]);
                };
                contextMenu.Items.Add(trimItem);
            }

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            foreach (var preset in presets)
            {
                var menuItem = new System.Windows.Controls.MenuItem { Header = preset.Name };

                string iconKey = !string.IsNullOrEmpty(preset.CustomIcon) 
                    ? preset.CustomIcon 
                    : (preset.IsAudioPreset ? "audio" : (preset.IsImagePreset ? "image" : "renderpard"));

                var presetIcon = iconConverter.Convert(iconKey, typeof(System.Windows.Media.ImageSource), string.Empty, System.Globalization.CultureInfo.CurrentCulture) as System.Windows.Media.ImageSource;
                if (presetIcon != null)
                {
                    menuItem.Icon = new System.Windows.Controls.Image
                    {
                        Source = presetIcon,
                        Width = 16,
                        Height = 16
                    };
                }

                menuItem.Click += (s, args) =>
                {
                    foreach (var file in files)
                    {
                        EnqueueFile(file, preset);
                    }
                    StartQueue();
                };
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.PlacementTarget = this;
            contextMenu.IsOpen = true;
        }
    }
}