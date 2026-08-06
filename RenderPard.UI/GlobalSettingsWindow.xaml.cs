using System.Windows;

namespace RenderPard.UI;

public partial class GlobalSettingsWindow : Window
{
    public GlobalSettingsWindow()
    {
        InitializeComponent();
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
