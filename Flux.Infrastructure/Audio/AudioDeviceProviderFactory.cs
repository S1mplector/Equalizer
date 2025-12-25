using System.Runtime.InteropServices;
using Flux.Application.Abstractions;

namespace Flux.Infrastructure.Audio;

/// <summary>
/// Factory for creating platform-appropriate audio device provider implementations.
/// </summary>
public static class AudioDeviceProviderFactory
{
    /// <summary>
    /// Creates an audio device provider appropriate for the current platform.
    /// </summary>
    public static IAudioDeviceProvider Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#if WINDOWS
            return new AudioDeviceProvider();
#else
            // When building on non-Windows, return a stub
            return new StubAudioDeviceProvider();
#endif
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOS.MacAudioDeviceProvider();
        }
        else
        {
            return new StubAudioDeviceProvider();
        }
    }
}

/// <summary>
/// Stub audio device provider for platforms without native implementation.
/// </summary>
internal sealed class StubAudioDeviceProvider : IAudioDeviceProvider
{
    public System.Collections.Generic.IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        return new System.Collections.Generic.List<AudioDeviceInfo>
        {
            new AudioDeviceInfo("default", "Default Audio Device", true)
        };
    }
}
