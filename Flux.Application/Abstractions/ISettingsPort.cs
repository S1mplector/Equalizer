using System.Threading.Tasks;
using Flux.Domain;

namespace Flux.Application.Abstractions;

public interface ISettingsPort
{
    Task<FluxSettings> GetAsync();
    Task SaveAsync(FluxSettings settings);
}
