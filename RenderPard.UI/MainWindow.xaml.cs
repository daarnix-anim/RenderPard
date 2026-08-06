using System.Windows;
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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // GitHub Updater check. 
        _ = GitHubUpdater.CheckForUpdatesAsync("daarnix-anim", "RenderPard", "1.0.0");
    }

    public void EnqueueFile(string filePath, Preset preset)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _viewModel.EnqueueFile(filePath, preset);
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