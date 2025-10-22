using System.Threading.Tasks;
using Equalizer.Domain;

namespace Equalizer.Application.Abstractions;

public interface ISettingsPort
{
    Task<EqualizerSettings> GetAsync();
    Task SaveAsync(EqualizerSettings settings);
}
