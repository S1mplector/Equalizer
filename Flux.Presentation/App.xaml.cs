using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Flux.Application.DependencyInjection;
using Flux.Infrastructure.DependencyInjection;
using Flux.Presentation.Overlay;
using Flux.Presentation.Tray;
using Flux.Presentation.Hotkeys;
using Flux.Presentation.Widgets;
using Flux.Application.Abstractions;
using Flux.Presentation.Splash;

namespace Flux.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    public static bool IsShuttingDown { get; private set; }
    private readonly object _logLock = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var splash = new SplashWindow();
        splash.Show();

        // Fire-and-forget async startup so we don't block the UI thread
        _ = InitializeAsync(splash);
    }

    private async Task InitializeAsync(SplashWindow splash)
    {
        try
        {
            splash.SetStatus("Building services...");

            // Small pause so the user can see each startup phase
            await Task.Delay(180);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddEqualizerApplication();
                    services.AddEqualizerInfrastructure();
                    services.AddSingleton<IOverlayManager, MultiMonitorOverlayManager>();
                    services.AddTransient(typeof(Overlay.OverlayWindow));
                    services.AddTransient(typeof(Settings.SettingsWindow));
                    services.AddHostedService<TrayIconHostedService>();
                    services.AddHostedService<GlobalHotkeyService>();
                    
                    // Widget system
                    services.AddSingleton<IWidgetRegistry>(sp =>
                    {
                        var registry = new WidgetRegistry();
                        registry.Register(new ClockWidgetRenderer());
                        registry.Register(new DateWidgetRenderer());
                        registry.Register(new SystemInfoWidgetRenderer());
                        return registry;
                    });
                    services.AddSingleton<WidgetManager>();
                    services.AddTransient<Settings.WidgetsWindow>();
                })
                .Build();

            splash.SetStatus("Starting background services...");
            await Task.Delay(180);
            await _host.StartAsync();

            splash.SetStatus("Loading settings and caching data...");
            await Task.Delay(180);
            var settingsPort = _host.Services.GetRequiredService<ISettingsPort>();
            var overlay = _host.Services.GetRequiredService<IOverlayManager>();
            var s = await settingsPort.GetAsync();

            if (s.OverlayVisible)
            {
                splash.SetStatus("Restoring overlay...");
                await Task.Delay(180);
                await overlay.ShowAsync();
            }

            splash.SetStatus("Ready");
            await Task.Delay(220);
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            splash.Dispatcher.Invoke(() => splash.Close());
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsShuttingDown = true;
        if (_host != null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        if (!IsShuttingDown)
        {
            System.Windows.MessageBox.Show(
                "Flux hit an unexpected error. A log was written to the logs folder in %AppData%/Flux.",
                "Flux error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
        LogException("UnhandledException", ex);
        if (!IsShuttingDown)
        {
            System.Windows.MessageBox.Show(
                "Flux hit an unexpected error and must close. See %AppData%/Flux/logs for details.",
                "Flux error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private void LogException(string source, Exception ex)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "Flux", "logs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"flux-{DateTime.UtcNow:yyyyMMdd}.log");
            var lines = new[]
            {
                $"[{DateTime.UtcNow:O}] {source}",
                ex.ToString(),
                new string('-', 60)
            };
            lock (_logLock)
            {
                File.AppendAllLines(file, lines);
            }
        }
        catch
        {
            // If logging fails, avoid crashing the app due to logging.
        }
    }
}

