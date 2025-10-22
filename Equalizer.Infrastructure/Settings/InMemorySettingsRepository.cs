using System.Threading.Tasks;
using Equalizer.Application.Abstractions;
using Equalizer.Domain;

namespace Equalizer.Infrastructure.Settings;

public sealed class InMemorySettingsRepository : ISettingsPort
{
    private EqualizerSettings _settings = EqualizerSettings.Default;

    public Task<EqualizerSettings> GetAsync() => Task.FromResult(_settings);

    public Task SaveAsync(EqualizerSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
