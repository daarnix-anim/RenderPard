using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace RenderPard.UI;

public partial class UpdateAvailableDialog : Window
{
    private readonly string? _htmlUrl;

    public bool UserWantsDownload { get; private set; }

    public UpdateAvailableDialog(string releaseTitle, string currentVersion, string newVersion, string releaseNotes, string? htmlUrl, bool hasExeAsset)
    {
        InitializeComponent();

        _htmlUrl = htmlUrl;

        ReleaseTitleText.Text = !string.IsNullOrWhiteSpace(releaseTitle) ? releaseTitle : $"RenderPard {newVersion}";
        CurrentVersionText.Text = currentVersion.StartsWith('v') ? currentVersion : $"v{currentVersion}";
        NewVersionText.Text = newVersion.StartsWith('v') ? newVersion : $"v{newVersion}";

        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            ReleaseNotesTextBlock.Text = "• Улучшения производительности и оптимизация алгоритмов обработки\n• Исправления обнаруженных ошибок и повышение стабильности работы";
        }
        else
        {
            ReleaseNotesTextBlock.Text = releaseNotes.Trim();
        }

        if (!hasExeAsset)
        {
            BtnDownload.Content = "Перейти к релизу ↗";
        }

        if (string.IsNullOrEmpty(_htmlUrl))
        {
            BtnGitHub.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        UserWantsDownload = true;
        DialogResult = true;
        Close();
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        UserWantsDownload = false;
        DialogResult = false;
        Close();
    }

    private void BtnGitHub_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_htmlUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _htmlUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open release URL: {ex.Message}");
            }
        }
    }
}
