using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace RenderPard.UI;

public partial class UpdateAvailableDialog : Window
{
    private readonly string? _htmlUrl;
    private readonly string? _downloadUrl;
    private readonly string _newVersion;
    private readonly bool _hasExeAsset;

    public enum DialogState
    {
        Notes,
        Downloading,
        Ready,
        Error
    }

    private DialogState _currentState = DialogState.Notes;

    public UpdateAvailableDialog(
        string releaseTitle, 
        string currentVersion, 
        string newVersion, 
        string releaseNotes, 
        string? htmlUrl, 
        string? downloadUrl = null, 
        bool hasExeAsset = true)
    {
        InitializeComponent();

        _htmlUrl = htmlUrl;
        _downloadUrl = downloadUrl;
        _newVersion = newVersion;
        _hasExeAsset = hasExeAsset;

        ReleaseTitleText.Text = !string.IsNullOrWhiteSpace(releaseTitle) ? releaseTitle : $"RenderPard {newVersion}";
        CurrentVersionText.Text = currentVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? currentVersion : $"v{currentVersion}";
        NewVersionText.Text = newVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? newVersion : $"v{newVersion}";

        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            ReleaseNotesTextBlock.Text = "• Улучшения производительности и оптимизация алгоритмов обработки\n• Исправления обнаруженных ошибок и повышение стабильности работы";
        }
        else
        {
            ReleaseNotesTextBlock.Text = releaseNotes.Trim();
        }

        if (!_hasExeAsset)
        {
            BtnStartDownload.Content = "Перейти к релизу ↗";
        }

        if (string.IsNullOrEmpty(_htmlUrl))
        {
            BtnGitHub.Visibility = Visibility.Collapsed;
        }

        this.Loaded += OnWindowLoaded;
        this.Unloaded += OnWindowUnloaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Hook up updater events
        GitHubUpdater.DownloadProgressChanged += OnDownloadProgressChanged;
        GitHubUpdater.UpdateReady += OnUpdateReady;
        GitHubUpdater.UpdateFailed += OnUpdateFailed;
        GitHubUpdater.DownloadCancelled += OnDownloadCancelled;

        // Sync with existing updater state
        if (GitHubUpdater.IsUpdateReady)
        {
            SetState(DialogState.Ready);
        }
        else if (GitHubUpdater.IsDownloading)
        {
            SetState(DialogState.Downloading);
            UpdateProgressDisplay(GitHubUpdater.CurrentProgress, GitHubUpdater.CurrentBytesRead, GitHubUpdater.CurrentTotalBytes, GitHubUpdater.CurrentSpeedBytesPerSec);
        }
        else
        {
            SetState(DialogState.Notes);
        }
    }

    private void OnWindowUnloaded(object sender, RoutedEventArgs e)
    {
        GitHubUpdater.DownloadProgressChanged -= OnDownloadProgressChanged;
        GitHubUpdater.UpdateReady -= OnUpdateReady;
        GitHubUpdater.UpdateFailed -= OnUpdateFailed;
        GitHubUpdater.DownloadCancelled -= OnDownloadCancelled;
    }

    public void SetState(DialogState state, string? errorMessage = null)
    {
        _currentState = state;

        NotesViewPanel.Visibility = state == DialogState.Notes ? Visibility.Visible : Visibility.Collapsed;
        ProgressViewPanel.Visibility = state != DialogState.Notes ? Visibility.Visible : Visibility.Collapsed;

        NotesButtonsPanel.Visibility = state == DialogState.Notes ? Visibility.Visible : Visibility.Collapsed;
        DownloadingButtonsPanel.Visibility = state == DialogState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        ReadyButtonsPanel.Visibility = state == DialogState.Ready ? Visibility.Visible : Visibility.Collapsed;
        ErrorButtonsPanel.Visibility = state == DialogState.Error ? Visibility.Visible : Visibility.Collapsed;

        BtnGitHub.Visibility = (state == DialogState.Notes && !string.IsNullOrEmpty(_htmlUrl)) ? Visibility.Visible : Visibility.Collapsed;
        BtnCancelDownload.Visibility = state == DialogState.Downloading ? Visibility.Visible : Visibility.Collapsed;

        switch (state)
        {
            case DialogState.Notes:
                HeroIconText.Text = "✨";
                TitleIconText.Text = "🚀";
                BadgeText.Text = "НОВЫЙ РЕЛИЗ";
                BadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xF3, 0x9C, 0x12));
                BadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xF3, 0x9C, 0x12));
                break;

            case DialogState.Downloading:
                HeroIconText.Text = "⬇";
                TitleIconText.Text = "⬇";
                BadgeText.Text = "СКАЧИВАНИЕ";
                BadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x34, 0x98, 0xDB));
                BadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x34, 0x98, 0xDB));
                ProgressStateTitle.Text = $"Загрузка обновления {_newVersion}...";
                StatusCardBorder.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x1A, 0x21));
                StatusCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x28, 0x2D, 0x37));
                StatusCardText.Text = "Вы можете свернуть окно в фон — загрузка продолжится автоматически.";
                break;

            case DialogState.Ready:
                HeroIconText.Text = "🎉";
                TitleIconText.Text = "✅";
                BadgeText.Text = "ГОТОВО К УСТАНОВКЕ";
                BadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x2E, 0xCC, 0x71));
                BadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x2E, 0xCC, 0x71));
                ProgressStateTitle.Text = $"Обновление {_newVersion} готово к установке!";
                UpdateProgressBar.Value = 1.0;
                ProgressPercentText.Text = "100%";
                ProgressPercentText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0xCC, 0x71));
                ProgressSpeedText.Text = "✅ Загрузка завершена";
                StatusCardBorder.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0x2E, 0xCC, 0x71));
                StatusCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x2E, 0xCC, 0x71));
                StatusCardText.Text = "Все компоненты загружены и проверены. Нажмите «Перезапустить и установить», чтобы применить обновление.";
                break;

            case DialogState.Error:
                HeroIconText.Text = "⚠️";
                TitleIconText.Text = "⚠️";
                BadgeText.Text = "ОШИБКА ЗАГРУЗКИ";
                BadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xE7, 0x4C, 0x3C));
                BadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xE7, 0x4C, 0x3C));
                ProgressStateTitle.Text = "Не удалось загрузить обновление";
                StatusCardBorder.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xE7, 0x4C, 0x3C));
                StatusCardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xE7, 0x4C, 0x3C));
                StatusCardText.Text = !string.IsNullOrEmpty(errorMessage) ? $"Ошибка: {errorMessage}" : "Произошла ошибка при загрузке. Проверьте интернет-соединение.";
                ProgressSpeedText.Text = "Загрузка прервана";
                break;
        }
    }

    private void UpdateProgressDisplay(double progress, long bytesRead, long totalBytes, double speedBytesPerSec)
    {
        UpdateProgressBar.Value = Math.Clamp(progress, 0, 1);
        ProgressPercentText.Text = $"{progress:P0}";
        ProgressPercentText.Foreground = (Brush)FindResource("AppLinkBrush");

        double mbRead = bytesRead / (1024.0 * 1024.0);
        double mbTotal = totalBytes > 0 ? totalBytes / (1024.0 * 1024.0) : 0;

        if (mbTotal > 0)
            ProgressBytesText.Text = $"{mbRead:0.0} / {mbTotal:0.0} МБ";
        else
            ProgressBytesText.Text = $"{mbRead:0.0} МБ";

        if (speedBytesPerSec > 0)
        {
            double mbSpeed = speedBytesPerSec / (1024.0 * 1024.0);
            long bytesRemaining = Math.Max(0, totalBytes - bytesRead);
            int secRemaining = (int)Math.Round(bytesRemaining / speedBytesPerSec);

            string etaText = secRemaining < 60 ? $"~{secRemaining} сек" : $"~{secRemaining / 60} мин {secRemaining % 60} сек";
            ProgressSpeedText.Text = $"⚡ {mbSpeed:0.0} МБ/с • Осталось: {etaText}";
        }
        else
        {
            ProgressSpeedText.Text = "Загрузка данных...";
        }
    }

    private void OnDownloadProgressChanged(double progress, long bytesRead, long totalBytes, double speedBytesPerSec)
    {
        if (_currentState != DialogState.Downloading)
        {
            SetState(DialogState.Downloading);
        }

        UpdateProgressDisplay(progress, bytesRead, totalBytes, speedBytesPerSec);
    }

    private void OnUpdateReady(string installerPath, string versionTag)
    {
        SetState(DialogState.Ready);
    }

    private void OnUpdateFailed(string error)
    {
        SetState(DialogState.Error, error);
    }

    private void OnDownloadCancelled()
    {
        SetState(DialogState.Notes);
    }

    private void BtnStartDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasExeAsset && !string.IsNullOrEmpty(_htmlUrl))
        {
            OpenBrowserUrl(_htmlUrl);
            Close();
            return;
        }

        if (!string.IsNullOrEmpty(_downloadUrl))
        {
            SetState(DialogState.Downloading);
            _ = GitHubUpdater.StartDownloadUpdateAsync(_downloadUrl, _newVersion);
        }
    }

    private void BtnRetryDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_downloadUrl))
        {
            SetState(DialogState.Downloading);
            _ = GitHubUpdater.StartDownloadUpdateAsync(_downloadUrl, _newVersion);
        }
    }

    private void BtnMinimizeToBackground_Click(object sender, RoutedEventArgs e)
    {
        // Dialog closes, but download keeps running in background
        Close();
    }

    private void BtnCancelDownload_Click(object sender, RoutedEventArgs e)
    {
        GitHubUpdater.CancelDownload();
    }

    private void BtnRestartNow_Click(object sender, RoutedEventArgs e)
    {
        GitHubUpdater.ApplyUpdateAndRestart();
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnGitHub_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_htmlUrl))
        {
            OpenBrowserUrl(_htmlUrl);
        }
    }

    private void OpenBrowserUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open browser: {ex.Message}");
        }
    }
}
