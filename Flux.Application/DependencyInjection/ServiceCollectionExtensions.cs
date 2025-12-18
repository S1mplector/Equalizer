using Microsoft.Extensions.DependencyInjection;
using Flux.Application.Abstractions;
using Flux.Application.Services;

namespace Flux.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEqualizerApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFluxService, FluxService>();
        services.AddSingleton<SpectrumProcessor>();
        return services;
    }
}
