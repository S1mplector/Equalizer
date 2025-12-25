using System;
using System.Threading;
using System.Threading.Tasks;
using Flux.Application.Services;
using Flux.Application.Models;

namespace Flux.Application.Abstractions;

public interface IFluxService
{
    Task<float[]> GetBarsAsync(CancellationToken cancellationToken);
    Task<VisualizerFrame> GetVisualizerFrameAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Event raised when spectrum data is updated (for UI binding).
    /// </summary>
    event Action<float[]>? SpectrumUpdated;
    
    /// <summary>
    /// Starts the audio processing loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Stops the audio processing loop.
    /// </summary>
    Task StopAsync();
}
