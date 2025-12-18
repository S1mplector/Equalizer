using System.Threading;
using System.Threading.Tasks;
using Flux.Application.Services;
using Flux.Application.Models;

namespace Flux.Application.Abstractions;

public interface IFluxService
{
    Task<float[]> GetBarsAsync(CancellationToken cancellationToken);
    Task<VisualizerFrame> GetVisualizerFrameAsync(CancellationToken cancellationToken);
}
