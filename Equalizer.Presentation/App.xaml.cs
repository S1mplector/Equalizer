using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Equalizer.Application.DependencyInjection;
using Equalizer.Infrastructure.DependencyInjection;
using Equalizer.Presentation.Overlay;
using Equalizer.Presentation.Tray;
using Equalizer.Presentation.Hotkeys;
using Equalizer.Application.Abstractions;

namespace Equalizer.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    public static bool IsShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddEqualizerApplication();
                services.AddEqualizerInfrastructure();
                services.AddSingleton<IOverlayManager, MultiMonitorOverlayManager>();
                services.AddTransient<Overlay.OverlayWindow>();
                services.AddTransient<Settings.SettingsWindow>();
                services.AddHostedService<TrayIconHostedService>();
                services.AddHostedService<GlobalHotkeyService>();
            })
            .Build();

        // Fire-and-forget async startup so we don't block the UI thread
        _ = StartHostAndRestoreOverlayAsync();
    }

    private async Task StartHostAndRestoreOverlayAsync()
    {
        if (_host == null) return;

        // Start host so hosted services (tray icon, hotkeys) run
        await _host.StartAsync();

        // Restore overlay visibility from last session
        var settingsPort = _host.Services.GetRequiredService<ISettingsPort>();
        var overlay = _host.Services.GetRequiredService<IOverlayManager>();
        var s = await settingsPort.GetAsync();
        if (s.OverlayVisible)
        {
            await overlay.ShowAsync();
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
}

