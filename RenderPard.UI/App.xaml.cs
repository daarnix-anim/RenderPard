using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RenderPard.Core;
using RenderPard.Core.Models;

namespace RenderPard.UI;

public partial class App : Application
{
    private const string MutexName = "RenderPard_SingleInstance_Mutex";
    private const string PipeName = "RenderPard_ArgsPipe";
    private Mutex? _mutex;
    private bool _ownsMutex;
    public static PresetManager PresetManager { get; } = new PresetManager();
    public static AppSettings Settings { get; set; } = AppSettingsManager.LoadSettings();

    protected override void OnStartup(StartupEventArgs e)
    {

        // Load language dictionary
        var langDict = new ResourceDictionary();
        string langFile = Settings.Language == "en-US" ? "Lang.en-US.xaml" : "Lang.ru-RU.xaml";
        langDict.Source = new Uri($"/RenderPard.UI;component/Themes/{langFile}", UriKind.RelativeOrAbsolute);
        this.Resources.MergedDictionaries.Add(langDict);

        _mutex = new Mutex(true, MutexName, out _ownsMutex);

        if (!_ownsMutex)
        {
            // Another instance is running, send args to it and exit
            SendArgsToRunningInstance(e.Args);
            Current.Shutdown();
            return;
        }

        // We are the first instance, start listening for args
        StartListeningForArgs();

        base.OnStartup(e);

        // Manually create the window because we removed StartupUri
        var mainWindow = new MainWindow();
        this.MainWindow = mainWindow;

        // Auto-register context menu to ensure it's always up-to-date
        try
        {
            RenderPard.Core.ContextMenuManager.Register(PresetManager.LoadPresets());
        }
        catch { }

        // Automatically update context menu icons when user switches between Windows Light/Dark theme
        try
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (sender, args) =>
            {
                if (args.Category == Microsoft.Win32.UserPreferenceCategory.General || 
                    args.Category == Microsoft.Win32.UserPreferenceCategory.Color)
                {
                    try
                    {
                        RenderPard.Core.ContextMenuManager.Register(PresetManager.LoadPresets());
                    }
                    catch { }
                }
            };
        }
        catch { }

        // Prevent WPF from shutting down if a dialog is shown and closed during ProcessArgs
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Process own args
        ProcessArgs(e.Args, isSecondInstance: false);

        mainWindow.Show();
        
        this.ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    private void StartListeningForArgs()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    string argsLine = await reader.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(argsLine))
                    {
                        var args = argsLine.Split('|');
                        Dispatcher.Invoke(() => ProcessArgs(args, isSecondInstance: true));
                    }
                }
                catch (Exception)
                {
                    // Ignore pipe errors
                }
            }
        });
    }

    private void SendArgsToRunningInstance(string[] args)
    {
        if (args.Length == 0) return;
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client);
            writer.Write(string.Join("|", args));
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    private void ProcessArgs(string[] args, bool isSecondInstance)
    {
        string presetName = null;
        string file = null;
        bool openApp = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--preset" && i + 1 < args.Length)
            {
                presetName = args[i + 1].Trim('"');
                i++;
            }
            else if (args[i] == "--file" && i + 1 < args.Length)
            {
                file = args[i + 1].Trim('"');
                i++;
            }
            else if (args[i] == "--trim" && i + 1 < args.Length)
            {
                string trimFile = args[i + 1].Trim('"');
                i++;
                if (MainWindow is MainWindow window)
                {
                    window.OpenTrimWindow(trimFile);
                    if (isSecondInstance)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            window.WindowState = WindowState.Normal;
                            window.Activate();
                        });
                    }
                }
                return;
            }
            else if (args[i] == "--open")
            {
                openApp = true;
            }
        }

        if (openApp || file == null)
        {
            if (isSecondInstance && MainWindow is MainWindow window)
            {
                Dispatcher.Invoke(() =>
                {
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                });
            }
        }

        if (file != null)
        {
            var presets = PresetManager.LoadPresets();
            var preset = presets.FirstOrDefault(p => p.Name == presetName) ?? presets.FirstOrDefault();
            
            if (preset != null)
            {
                if (Directory.Exists(file))
                {
                    var supportedExts = new System.Collections.Generic.HashSet<string>(RenderPard.Core.ContextMenuManager.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
                    var allFiles = new System.Collections.Generic.List<string>();
                    
                    var cts = new CancellationTokenSource();
                    var progressWindow = new ScanningProgressWindow(cts);
                    
                    if (MainWindow != null && MainWindow.IsVisible)
                        progressWindow.Owner = MainWindow;
                    else
                        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    var scanTask = Task.Run(() =>
                    {
                        int foundCount = 0;
                        var dirs = new System.Collections.Generic.Stack<string>();
                        dirs.Push(file);
                        long lastUpdate = Environment.TickCount64;

                        while (dirs.Count > 0)
                        {
                            if (cts.Token.IsCancellationRequested) return;
                            string currentDir = dirs.Pop();
                            
                            if (Environment.TickCount64 - lastUpdate > 50)
                            {
                                Dispatcher.InvokeAsync(() => progressWindow.UpdateProgress(currentDir, foundCount));
                                lastUpdate = Environment.TickCount64;
                            }

                            try
                            {
                                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                                    dirs.Push(subDir);
                                
                                foreach (var f in Directory.EnumerateFiles(currentDir, "*.*"))
                                {
                                    if (cts.Token.IsCancellationRequested) return;
                                    if (supportedExts.Contains(Path.GetExtension(f)))
                                    {
                                        allFiles.Add(f);
                                        foundCount++;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException) { }
                            catch (Exception) { }
                        }
                    }, cts.Token);

                    scanTask.ContinueWith(t => Dispatcher.InvokeAsync(() => progressWindow.Close()));

                    progressWindow.ShowDialog();

                    if (Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown)
                        Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

                    if (cts.Token.IsCancellationRequested)
                        return;
                    
                    if (allFiles.Count == 0)
                    {
                        MessageBox.Show("Не найдено поддерживаемых медиафайлов в этой папке.", "RenderPard", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var extCounts = allFiles.GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                                            .ToDictionary(g => g.Key, g => g.Count());
                    
                    Dispatcher.Invoke(() => 
                    {
                        var dialog = new FolderImportDialog(extCounts);
                        if (dialog.ShowDialog() == true)
                        {
                            var selectedExts = new System.Collections.Generic.HashSet<string>(dialog.SelectedExtensions, StringComparer.OrdinalIgnoreCase);
                            var filesToAdd = allFiles.Where(f => selectedExts.Contains(Path.GetExtension(f))).ToList();
                            
                            if (MainWindow is MainWindow window)
                            {
                                foreach(var f in filesToAdd)
                                {
                                    window.EnqueueFile(f, preset);
                                }
                                window.StartQueue();
                                if (isSecondInstance) 
                                { 
                                    window.WindowState = WindowState.Normal; 
                                    window.Activate(); 
                                }
                            }
                        }
                    });
                }
                else if (File.Exists(file))
                {
                    if (MainWindow is MainWindow window)
                    {
                        window.EnqueueFile(file, preset);
                        window.StartQueue();
                        
                        if (isSecondInstance)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                window.WindowState = WindowState.Normal;
                                window.Activate();
                            });
                        }
                    }
                }
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex && _mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
