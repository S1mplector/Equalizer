using Microsoft.Extensions.DependencyInjection;
using Flux.Application.Abstractions;
using Flux.Infrastructure.Audio;
using Flux.Infrastructure.Platform;
using Flux.Infrastructure.Settings;
using Flux.Infrastructure.Widgets;

namespace Flux.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEqualizerInfrastructure(this IServiceCollection services)
    {
        // Use cross-platform factories for audio
        services.AddSingleton<IAudioInputPort>(sp =>
        {
            var settings = sp.GetService<ISettingsPort>()?.GetAsync().GetAwaiter().GetResult();
            return AudioInputFactory.Create(settings?.AudioDeviceId);
        });
        services.AddSingleton<IAudioDeviceProvider>(sp => AudioDeviceProviderFactory.Create());
        
        // Platform info
        services.AddSingleton<IPlatformInfo, PlatformInfo>();
        
        // Settings and widgets
        services.AddSingleton<ISettingsPort, JsonSettingsRepository>();
        services.AddSingleton<IWidgetLayoutPort, JsonWidgetLayoutRepository>();
        return services;
    }
}
