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

        // Process own args
        ProcessArgs(e.Args, isSecondInstance: false);

        mainWindow.Show();
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
                presetName = args[i + 1];
                i++;
            }
            else if (args[i] == "--file" && i + 1 < args.Length)
            {
                file = args[i + 1];
                i++;
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
            
            if (preset != null && MainWindow is MainWindow window)
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
