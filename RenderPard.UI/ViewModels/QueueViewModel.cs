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

    [ObservableProperty]
    private double _totalProgress;

    [ObservableProperty]
    private bool _isFfmpegMissing;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    public QueueViewModel()
    {
        _ffmpeg = new FFmpegWrapper(); // Assumes ffmpeg is in PATH or current dir
        
        CheckFfmpeg();
        
        StartQueueProcessor();
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

        string targetDir = Path.Combine(Path.GetDirectoryName(filePath)!, preset.Name);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string originalName = Path.GetFileNameWithoutExtension(filePath);
        string extension = preset.Container == ContainerFormat.WebM ? ".webm" : ".mp4";
        string safePresetName = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars())).ToLower();
        string targetFile = Path.Combine(targetDir, $"{originalName}_{safePresetName}{extension}");

        // Handle duplication (e.g., _01)
        int counter = 1;
        while (File.Exists(targetFile))
        {
            targetFile = Path.Combine(targetDir, $"{originalName}_{safePresetName}_{counter:D2}{extension}");
            counter++;
        }

        var task = new TranscodeTask
        {
            SourceFilePath = filePath,
            TargetFilePath = targetFile,
            Preset = preset
        };

        Tasks.Add(task);
    }

    private void StartQueueProcessor()
    {
        Task.Run(async () =>
        {
            while (!_globalCts.Token.IsCancellationRequested)
            {
                TranscodeTask? nextTask = null;
                
                // Find next pending task
                foreach (var task in Tasks)
                {
                    if (task.Status == TranscodeTaskStatus.Pending && !IsFfmpegMissing && !IsDownloading)
                    {
                        nextTask = task;
                        break;
                    }
                }

                if (nextTask != null)
                {
                    nextTask.Status = TranscodeTaskStatus.Probing;
                    await _ffmpeg.ProbeTaskAsync(nextTask);

                    if (nextTask.Status != TranscodeTaskStatus.Failed)
                    {
                        await _ffmpeg.RunEncodeAsync(nextTask, _globalCts.Token);
                    }
                }
                else
                {
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
    private void RegisterMenu()
    {
        var presets = App.PresetManager.LoadPresets();
        ContextMenuManager.Register(presets);
        System.Windows.MessageBox.Show("Context menu registered successfully.");
    }

    [RelayCommand]
    private void UnregisterMenu()
    {
        ContextMenuManager.Unregister();
        System.Windows.MessageBox.Show("Context menu removed successfully.");
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.ShowDialog();
        
        // After closing, we don't automatically do anything here because the SaveCommand inside SettingsViewModel 
        // already handles saving and re-registering the menu. We might want to reload presets into memory here if needed.
    }
}
