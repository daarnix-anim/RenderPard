using System.Threading;
using System.Windows;
using System.IO;

namespace RenderPard.UI;

public partial class AddingProgressWindow : Window
{
    private readonly CancellationTokenSource _cts;
    private readonly int _totalFiles;

    public AddingProgressWindow(CancellationTokenSource cts, int totalFiles)
    {
        InitializeComponent();
        _cts = cts;
        _totalFiles = totalFiles;
        ProgressBar.Maximum = _totalFiles;
        ProgressText.Text = $"Добавлено: 0 / {_totalFiles}";
    }

    public void UpdateProgress(string currentFile, int addedCount)
    {
        CurrentFileText.Text = Path.GetFileName(currentFile);
        ProgressBar.Value = addedCount;
        ProgressText.Text = $"Добавлено: {addedCount} / {_totalFiles}";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        CancelBtn.IsEnabled = false;
        CancelBtn.Content = "Отмена...";
    }
}
