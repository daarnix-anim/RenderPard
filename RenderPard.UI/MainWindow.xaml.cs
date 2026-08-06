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
        _viewModel = new QueueViewModel();
        DataContext = _viewModel;
        
        this.Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // GitHub Updater check. 
        _ = GitHubUpdater.CheckForUpdatesAsync("daarnix-anim", "RenderPard", "1.0.0");
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
}