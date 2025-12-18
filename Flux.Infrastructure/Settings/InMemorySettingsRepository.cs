using System.Threading.Tasks;
using Flux.Application.Abstractions;
using Flux.Domain;

namespace Flux.Infrastructure.Settings;

public sealed class InMemorySettingsRepository : ISettingsPort
{
    private FluxSettings _settings = FluxSettings.Default;

    public Task<FluxSettings> GetAsync() => Task.FromResult(_settings);

    public Task SaveAsync(FluxSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
