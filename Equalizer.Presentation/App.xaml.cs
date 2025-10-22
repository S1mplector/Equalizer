using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Equalizer.Application.DependencyInjection;
using Equalizer.Infrastructure.DependencyInjection;

namespace Equalizer.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddEqualizerApplication();
                services.AddEqualizerInfrastructure();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}

