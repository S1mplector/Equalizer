using System.Collections.Generic;
using System.Runtime.InteropServices;
using Flux.Application.Abstractions;

namespace Flux.Infrastructure.Audio.MacOS;

/// <summary>
/// macOS audio device provider using CoreAudio.
/// </summary>
public sealed class MacAudioDeviceProvider : IAudioDeviceProvider
{
    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new List<AudioDeviceInfo>();
        }

        // On macOS, we return a simplified device list
        // Full implementation would use AudioObjectGetPropertyData to enumerate devices
        var devices = new List<AudioDeviceInfo>
        {
            new AudioDeviceInfo(
                Id: "default",
                Name: "System Audio (Default)",
                IsDefault: true
            )
        };

        // TODO: Implement full CoreAudio device enumeration
        // This requires P/Invoke to AudioToolbox framework:
        // - AudioObjectGetPropertyDataSize
        // - AudioObjectGetPropertyData
        // - kAudioHardwarePropertyDevices
        // - kAudioDevicePropertyDeviceNameCFString

        return devices;
    }
}
