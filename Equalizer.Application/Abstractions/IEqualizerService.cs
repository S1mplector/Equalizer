using System.Threading;
using System.Threading.Tasks;
using Equalizer.Application.Services;
using Equalizer.Application.Models;

namespace Equalizer.Application.Abstractions;

public interface IEqualizerService
{
    Task<float[]> GetBarsAsync(CancellationToken cancellationToken);
    Task<VisualizerFrame> GetVisualizerFrameAsync(CancellationToken cancellationToken);
}
