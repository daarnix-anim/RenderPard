using System.Threading;
using System.Windows;

namespace RenderPard.UI;

public partial class ScanningProgressWindow : Window
{
    private readonly CancellationTokenSource _cts;

    public ScanningProgressWindow(CancellationTokenSource cts)
    {
        InitializeComponent();
        _cts = cts;
    }

    public void UpdateProgress(string currentFolder, int filesFound)
    {
        CurrentFolderText.Text = "Сканируется: " + currentFolder;
        FilesFoundText.Text = $"Найдено файлов: {filesFound}";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        CancelBtn.IsEnabled = false;
        CancelBtn.Content = "Отмена...";
    }
}
