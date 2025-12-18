using Microsoft.Extensions.DependencyInjection;
using Flux.Application.Abstractions;
using Flux.Infrastructure.Audio;
using Flux.Infrastructure.Settings;
using Flux.Infrastructure.Widgets;

namespace Flux.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEqualizerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAudioInputPort, WASAPILoopbackAudioInput>();
        services.AddSingleton<ISettingsPort, JsonSettingsRepository>();
        services.AddSingleton<IAudioDeviceProvider, AudioDeviceProvider>();
        services.AddSingleton<IWidgetLayoutPort, JsonWidgetLayoutRepository>();
        return services;
    }
}
