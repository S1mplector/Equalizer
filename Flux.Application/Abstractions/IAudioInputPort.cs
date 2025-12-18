using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flux.Application.Audio;

namespace Flux.Application.Abstractions;

public interface IAudioInputPort
{
    Task<AudioFrame> ReadFrameAsync(int minSamples, CancellationToken cancellationToken);
}

public record AudioDeviceInfo(string Id, string Name, bool IsDefault);

public interface IAudioDeviceProvider
{
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
}
