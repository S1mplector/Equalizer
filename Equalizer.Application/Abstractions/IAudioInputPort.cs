using System.Threading;
using System.Threading.Tasks;

namespace Equalizer.Application.Abstractions;

public interface IAudioInputPort
{
    Task<float[]> GetSpectrumAsync(int bars, CancellationToken cancellationToken);
}
