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

    protected override void OnStartup(StartupEventArgs e)
    {
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

        // Process own args
        ProcessArgs(e.Args);
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
                        Dispatcher.Invoke(() => ProcessArgs(args));
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

    private void ProcessArgs(string[] args)
    {
        string presetName = null;
        string file = null;

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
        }

        if (file != null)
        {
            var presets = PresetManager.LoadPresets();
            var preset = presets.FirstOrDefault(p => p.Name == presetName) ?? presets.FirstOrDefault();
            
            if (preset != null && MainWindow is MainWindow window)
            {
                // Assuming MainWindow has a ViewModel we can interact with
                // For now, let's just create a generic event or call a method
                window.EnqueueFile(file, preset);
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
