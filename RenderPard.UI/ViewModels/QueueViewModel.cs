using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RenderPard.Core;
using RenderPard.Core.Models;

namespace RenderPard.UI.ViewModels;

public partial class QueueViewModel : ObservableObject
{
    private readonly FFmpegWrapper _ffmpeg;
    private readonly CancellationTokenSource _globalCts = new();

    public ObservableCollection<TranscodeTask> Tasks { get; } = new();

    public string AppVersion => "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.4.1");

    [ObservableProperty]
    private double _totalProgress;

    public bool AlwaysOnTop
    {
        get => App.Settings.AlwaysOnTop;
        set
        {
            if (App.Settings.AlwaysOnTop != value)
            {
                App.Settings.AlwaysOnTop = value;
                AppSettingsManager.SaveSettings(App.Settings);
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    private bool _isFfmpegMissing;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private bool _isQueueActive;

    [ObservableProperty]
    private bool _isMenuRegistered;

    // Background In-App Update Properties (Telegram-style)
    [ObservableProperty]
    private bool _isUpdateDownloading;

    [ObservableProperty]
    private double _updateDownloadProgress;

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    private bool _isUpdateReady;

    [ObservableProperty]
    private string _updateReadyText = string.Empty;

    private static OverwriteResult? _currentBatchOverwriteResult = null;
    private static DateTime _lastBatchTime = DateTime.MinValue;

    public bool CreateSubfolderForFiles
    {
        get => App.Settings.CreateSubfolderForFiles;
        set
        {
            if (App.Settings.CreateSubfolderForFiles != value)
            {
                App.Settings.CreateSubfolderForFiles = value;
                AppSettingsManager.SaveSettings(App.Settings);
                OnPropertyChanged();
            }
        }
    }

    public QueueViewModel()
    {
        _ffmpeg = new FFmpegWrapper(); // Assumes ffmpeg is in PATH or current dir
        
        CheckFfmpeg();
        
        IsMenuRegistered = ContextMenuManager.IsRegistered();
        
        // Subscribe to live GitHub background updater events
        GitHubUpdater.DownloadProgressChanged += OnUpdateDownloadProgressChanged;
        GitHubUpdater.UpdateReady += OnUpdateReady;
        GitHubUpdater.UpdateFailed += OnUpdateFailed;
        GitHubUpdater.DownloadCancelled += OnUpdateCancelled;

        StartQueueProcessor();
    }

    private void OnUpdateDownloadProgressChanged(double progress, long bytesRead, long totalBytes, double speedBytesPerSec)
    {
        IsUpdateDownloading = true;
        IsUpdateReady = false;
        UpdateDownloadProgress = progress;

        double mbRead = bytesRead / (1024.0 * 1024.0);
        double mbTotal = totalBytes > 0 ? totalBytes / (1024.0 * 1024.0) : 0;
        double mbSpeed = speedBytesPerSec / (1024.0 * 1024.0);

        string tag = GitHubUpdater.LatestVersionTag ?? "новой версии";
        if (mbTotal > 0 && mbSpeed > 0.05)
        {
            UpdateStatusText = $"Загрузка {tag}: {mbRead:0.0}/{mbTotal:0.0} МБ ({progress:P0}) • {mbSpeed:0.0} МБ/с";
        }
        else if (mbTotal > 0)
        {
            UpdateStatusText = $"Загрузка {tag}: {mbRead:0.0}/{mbTotal:0.0} МБ ({progress:P0})";
        }
        else
        {
            UpdateStatusText = $"Загрузка {tag}: {mbRead:0.0} МБ";
        }
    }

    private void OnUpdateReady(string installerPath, string versionTag)
    {
        IsUpdateDownloading = false;
        IsUpdateReady = true;
        UpdateReadyText = $"Обновление {versionTag} готово";
    }

    private void OnUpdateFailed(string error)
    {
        IsUpdateDownloading = false;
        IsUpdateReady = false;
    }

    private void OnUpdateCancelled()
    {
        IsUpdateDownloading = false;
        IsUpdateReady = false;
    }

    [RelayCommand]
    private void OpenUpdateDetails()
    {
        GitHubUpdater.OpenUpdateDialog();
    }

    [RelayCommand]
    private void CancelUpdateDownload()
    {
        GitHubUpdater.CancelDownload();
    }

    [RelayCommand]
    private void ApplyUpdateAndRestart()
    {
        GitHubUpdater.ApplyUpdateAndRestart();
    }

    private void CheckFfmpeg()
    {
        string currentDir = System.AppDomain.CurrentDomain.BaseDirectory;
        if (!FfmpegDownloader.CheckFfmpegExists(currentDir))
        {
            IsFfmpegMissing = true;
        }
    }

    [RelayCommand]
    private async Task DownloadFfmpegAsync()
    {
        IsFfmpegMissing = false;
        IsDownloading = true;
        DownloadStatus = "Downloading FFmpeg from gyan.dev...";
        DownloadProgress = 0;

        try
        {
            var downloader = new FfmpegDownloader();
            var progress = new System.Progress<double>(p => 
            {
                DownloadProgress = p;
                if (p >= 1.0) DownloadStatus = "Extracting files...";
            });

            string currentDir = System.AppDomain.CurrentDomain.BaseDirectory;
            await downloader.DownloadAndExtractAsync(currentDir, progress, _globalCts.Token);
            
            IsDownloading = false;
            // Success, can start processing normally
        }
        catch (System.Exception ex)
        {
            IsDownloading = false;
            IsFfmpegMissing = true;
            System.Windows.MessageBox.Show($"Failed to download FFmpeg: {ex.Message}");
        }
    }

    public void EnqueueFile(string filePath, Preset preset)
    {
        if (!File.Exists(filePath)) return;

        string ext = Path.GetExtension(filePath).ToLower();
        if (ext == ".ai" || ext == ".pdf")
        {
            Task.Run(() =>
            {
                try
                {
                    var tempImages = PdfExtractor.ExtractPages(filePath);
                    string suffix = ext == ".ai" ? "Artboard" : "Page";
                    for (int i = 0; i < tempImages.Count; i++)
                    {
                        var pageImg = tempImages[i];
                        string pageOriginalName = $"{Path.GetFileNameWithoutExtension(filePath)}_{suffix}{i + 1}";
                        EnqueueSingleFile(pageImg, filePath, preset, pageOriginalName, true);
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to extract pages from {Path.GetFileName(filePath)}:\n{ex.Message}");
                }
            });
        }
        else if (ext == ".cr2" || ext == ".nef" || ext == ".arw" || ext == ".dng" || ext == ".heic")
        {
            Task.Run(() =>
            {
                try
                {
                    string tempImg = RenderPard.Core.RawImageExtractor.ExtractToTempPng(filePath);
                    string originalName = Path.GetFileNameWithoutExtension(filePath);
                    EnqueueSingleFile(tempImg, filePath, preset, originalName, true);
                }
                catch (System.Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show($"Failed to process RAW/HEIC image {Path.GetFileName(filePath)}:\n{ex.Message}");
                    });
                }
            });
        }
        else
        {
            EnqueueSingleFile(filePath, filePath, preset, Path.GetFileNameWithoutExtension(filePath), false);
        }
    }

    private void EnqueueSingleFile(string sourceFile, string originalFileForDir, Preset preset, string originalName, bool isTempSource)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => 
        {
            // Reset batch overwrite state if it's been more than 2 seconds since the last file was added
            if ((DateTime.Now - _lastBatchTime).TotalSeconds > 2)
            {
                _currentBatchOverwriteResult = null;
            }
            _lastBatchTime = DateTime.Now;

            string targetDir = App.Settings.CreateSubfolderForFiles 
                ? Path.Combine(Path.GetDirectoryName(originalFileForDir)!, preset.Name)
                : Path.GetDirectoryName(originalFileForDir)!;
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string extension = preset.Container switch
            {
                ContainerFormat.WebM => ".webm",
                ContainerFormat.Gif => ".gif",
                ContainerFormat.Jpeg => ".jpg",
                ContainerFormat.Png => ".png",
                ContainerFormat.Webp => ".webp",
                ContainerFormat.MXF => ".mxf",
                ContainerFormat.Mp3 => ".mp3",
                ContainerFormat.Wav => ".wav",
                ContainerFormat.Ogg => ".ogg",
                ContainerFormat.Flac => ".flac",
                ContainerFormat.Aac => ".m4a",
                _ => ".mp4"
            };

            string safePresetName = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars())).ToLower();
            
            string baseFileName = originalName;
            switch (preset.NamingLogic)
            {
                case NamingMode.Suffix:
                    baseFileName = $"{originalName}_{safePresetName}";
                    break;
                case NamingMode.Prefix:
                    baseFileName = $"{safePresetName}_{originalName}";
                    break;
                case NamingMode.NoChange:
                    baseFileName = originalName;
                    break;
            }
            
            string targetFile = Path.Combine(targetDir, $"{baseFileName}{extension}");

            int counter = 1;
            
            string GetNumberedName(int c) => preset.NumberingLogic == Core.Models.NamingMode.Prefix ? $"{c:D2}_{baseFileName}" : $"{baseFileName}_{c:D2}";
            
            if (preset.NumberingLogic != Core.Models.NamingMode.NoChange)
            {
                // Numbering always enforced
                while (File.Exists(Path.Combine(targetDir, $"{GetNumberedName(counter)}{extension}")))
                {
                    counter++;
                }
                targetFile = Path.Combine(targetDir, $"{GetNumberedName(counter)}{extension}");
            }
            else
            {
                if (File.Exists(targetFile))
                {
                    if (targetFile.Equals(originalFileForDir, StringComparison.OrdinalIgnoreCase))
                    {
                        System.Windows.MessageBox.Show($"Файл {Path.GetFileName(targetFile)} совпадает с исходником! Невозможно перезаписать исходный файл. Пожалуйста, включите создание подпапок или измените логику названия.", "Критическая ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return; // Abort
                    }

                    if (!App.Settings.CreateSubfolderForFiles)
                    {
                        if (_currentBatchOverwriteResult == OverwriteResult.NoToAll) return;

                        if (_currentBatchOverwriteResult != OverwriteResult.YesToAll)
                        {
                            var dialog = new OverwriteDialog($"Файл '{Path.GetFileName(targetFile)}' уже существует в папке. Перезаписать?");
                            dialog.ShowDialog();
                            _currentBatchOverwriteResult = dialog.Result;

                            if (_currentBatchOverwriteResult == OverwriteResult.No || _currentBatchOverwriteResult == OverwriteResult.NoToAll)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Auto-increment to avoid collision
                        while (File.Exists(targetFile))
                        {
                            targetFile = Path.Combine(targetDir, $"{GetNumberedName(counter)}{extension}");
                            counter++;
                        }
                    }
                }
            }

            var task = new TranscodeTask
            {
                SourceFilePath = sourceFile,
                TargetFilePath = targetFile,
                Preset = preset,
                OriginalFileName = originalName + Path.GetExtension(originalFileForDir),
                IsTempSource = isTempSource
            };

            Tasks.Add(task);
        });
    }

    private void StartQueueProcessor()
    {
        Task.Run(async () =>
        {
            bool wasProcessing = false;
            string? lastOutputDir = null;

            while (!_globalCts.Token.IsCancellationRequested)
            {
                TranscodeTask? nextTask = null;
                bool hasPendingOrEncoding = false;
                
                // Find next pending task
                if (IsQueueActive)
                {
                    foreach (var task in Tasks)
                    {
                        if (task.Status == TranscodeTaskStatus.Pending || task.Status == TranscodeTaskStatus.Encoding || task.Status == TranscodeTaskStatus.Probing)
                        {
                            hasPendingOrEncoding = true;
                        }
                        if (task.Status == TranscodeTaskStatus.Pending && !IsFfmpegMissing && !IsDownloading)
                        {
                            if (nextTask == null) nextTask = task;
                        }
                    }
                }

                if (nextTask != null)
                {
                    wasProcessing = true;
                    lastOutputDir = Path.GetDirectoryName(nextTask.TargetFilePath);

                    nextTask.Status = TranscodeTaskStatus.Probing;
                    await _ffmpeg.ProbeTaskAsync(nextTask);

                    if (nextTask.Status != TranscodeTaskStatus.Failed)
                    {
                        await _ffmpeg.RunEncodeAsync(nextTask, _globalCts.Token);
                    }

                    if (nextTask.IsTempSource && File.Exists(nextTask.SourceFilePath))
                    {
                        try { File.Delete(nextTask.SourceFilePath); } catch { }
                    }
                }
                else
                {
                    if (wasProcessing && !hasPendingOrEncoding)
                    {
                        wasProcessing = false;
                        if (App.Settings.OpenFolderOnCompletion && App.Settings.CreateSubfolderForFiles && !string.IsNullOrEmpty(lastOutputDir) && Directory.Exists(lastOutputDir))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", lastOutputDir);
                        }
                    }
                    await Task.Delay(1000, _globalCts.Token);
                }
            }
        });
    }

    [RelayCommand]
    private void CancelAll()
    {
        _globalCts.Cancel();
    }

    [RelayCommand]
    private void ToggleQueue()
    {
        IsQueueActive = !IsQueueActive;
    }

    [RelayCommand]
    private void RemoveTask(TranscodeTask task)
    {
        if (task != null)
        {
            Tasks.Remove(task);
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        var toRemove = new System.Collections.Generic.List<TranscodeTask>();
        foreach(var task in Tasks)
        {
            if (task.Status != TranscodeTaskStatus.Probing && task.Status != TranscodeTaskStatus.Encoding)
            {
                toRemove.Add(task);
            }
        }
        foreach(var t in toRemove)
        {
            Tasks.Remove(t);
        }
    }

    [RelayCommand]
    private void UndoAll()
    {
        var toRemove = new System.Collections.Generic.List<TranscodeTask>();
        foreach (var task in Tasks)
        {
            if (task.Status == TranscodeTaskStatus.Completed)
            {
                try
                {
                    if (File.Exists(task.TargetFilePath))
                    {
                        File.Delete(task.TargetFilePath);
                    }
                }
                catch { } // Ignore file locked exceptions
                toRemove.Add(task);
            }
        }
        foreach (var t in toRemove)
        {
            Tasks.Remove(t);
        }
    }

    [RelayCommand]
    private void UndoTask(TranscodeTask task)
    {
        if (task == null) return;
        if (task.Status == TranscodeTaskStatus.Completed)
        {
            try
            {
                if (File.Exists(task.TargetFilePath))
                {
                    File.Delete(task.TargetFilePath);
                }
            }
            catch { }
        }
        Tasks.Remove(task);
    }

    [RelayCommand]
    private void ToggleMenu()
    {
        if (IsMenuRegistered)
        {
            ContextMenuManager.Unregister();
            IsMenuRegistered = false;
        }
        else
        {
            var presets = App.PresetManager.LoadPresets();
            ContextMenuManager.Register(presets);
            IsMenuRegistered = true;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = App.Current.MainWindow;
        settingsWindow.ShowDialog();
    }

    [RelayCommand]
    private void OpenGlobalSettings()
    {
        var globalSettingsWindow = new GlobalSettingsWindow();
        globalSettingsWindow.Owner = App.Current.MainWindow;
        globalSettingsWindow.ShowDialog();
    }

    [RelayCommand]
    public void OpenQuickTrim()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Media files (*.mp4;*.mov;*.mkv;*.avi;*.webm;*.mp3;*.wav;*.ogg;*.flac;*.m4a)|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.mp3;*.wav;*.ogg;*.flac;*.m4a|All files (*.*)|*.*",
            Title = "Выберите медиафайл для обрезки"
        };

        if (dialog.ShowDialog() == true)
        {
            OpenTrimWindowForFile(dialog.FileName);
        }
    }

    public void OpenTrimWindowForFile(string filePath, Preset? initialPreset = null)
    {
        var trimWindow = new TrimWindow(filePath, initialPreset)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (trimWindow.ShowDialog() == true && trimWindow.CreatedTasks.Count > 0)
        {
            foreach (var t in trimWindow.CreatedTasks)
            {
                Tasks.Add(t);
            }
            if (trimWindow.StartImmediately && !IsQueueActive)
            {
                IsQueueActive = true;
            }
        }
    }

    [RelayCommand]
    public void TrimTask(TranscodeTask task)
    {
        if (task == null || string.IsNullOrEmpty(task.SourceFilePath)) return;

        var trimWindow = new TrimWindow(task.SourceFilePath, task.Preset)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (trimWindow.ShowDialog() == true && trimWindow.CreatedTasks.Count > 0)
        {
            var first = trimWindow.CreatedTasks[0];
            task.TrimStartSeconds = first.TrimStartSeconds;
            task.TrimEndSeconds = first.TrimEndSeconds;
            task.Preset = first.Preset;
            task.IsLosslessCopy = first.IsLosslessCopy;
            task.TargetFilePath = first.TargetFilePath;
            task.Segments = first.Segments;
            task.IsCropped = first.IsCropped;
            task.CropX = first.CropX;
            task.CropY = first.CropY;
            task.CropWidth = first.CropWidth;
            task.CropHeight = first.CropHeight;
            task.Status = TranscodeTaskStatus.Pending;

            for (int i = 1; i < trimWindow.CreatedTasks.Count; i++)
            {
                Tasks.Add(trimWindow.CreatedTasks[i]);
            }

            if (trimWindow.StartImmediately && !IsQueueActive)
            {
                IsQueueActive = true;
            }
        }
    }

    [RelayCommand]
    private void SwitchLanguage(string lang)
    {
        if (App.Settings.Language != lang)
        {
            App.Settings.Language = lang;
            AppSettingsManager.SaveSettings(App.Settings);
            
            var uri = new System.Uri($"/RenderPard.UI;component/Themes/Lang.{lang}.xaml", System.UriKind.RelativeOrAbsolute);
            var appResources = System.Windows.Application.Current.Resources;
            var oldDict = System.Linq.Enumerable.FirstOrDefault(appResources.MergedDictionaries, d => d.Source != null && d.Source.OriginalString.Contains("Lang."));
            if (oldDict != null)
            {
                appResources.MergedDictionaries.Remove(oldDict);
            }
            appResources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = uri });
        }
    }
}
