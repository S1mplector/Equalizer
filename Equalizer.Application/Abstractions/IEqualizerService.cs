using System.Threading;
using System.Threading.Tasks;

namespace Equalizer.Application.Abstractions;

public interface IEqualizerService
{
    Task<float[]> GetBarsAsync(CancellationToken cancellationToken);
}
