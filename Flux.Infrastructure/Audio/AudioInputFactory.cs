using System;
using System.Runtime.InteropServices;
using Flux.Application.Abstractions;

namespace Flux.Infrastructure.Audio;

/// <summary>
/// Factory for creating platform-appropriate audio input implementations.
/// </summary>
public static class AudioInputFactory
{
    /// <summary>
    /// Creates an audio input instance appropriate for the current platform.
    /// </summary>
    /// <param name="deviceId">Optional device ID. Null uses default device.</param>
    /// <returns>Platform-specific IAudioInputPort implementation.</returns>
    public static IAudioInputPort Create(string? deviceId = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CreateWindowsAudioInput(deviceId);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOS.CoreAudioInput(deviceId);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // TODO: Implement PulseAudio/PipeWire capture for Linux
            // For now, fall back to random audio for visualization testing
            return new RandomAudioInput();
        }
        else
        {
            // Unknown platform - use random audio for testing
            return new RandomAudioInput();
        }
    }

    private static IAudioInputPort CreateWindowsAudioInput(string? deviceId)
    {
        // Use reflection to avoid compile-time dependency on Windows-only types
        // when building on non-Windows platforms
        var type = Type.GetType("Flux.Infrastructure.Audio.WASAPILoopbackAudioInput, Flux.Infrastructure");
        if (type != null)
        {
            var instance = Activator.CreateInstance(type, deviceId);
            if (instance is IAudioInputPort audioInput)
            {
                return audioInput;
            }
        }
        
        // Fallback if Windows types aren't available
        return new RandomAudioInput();
    }

    /// <summary>
    /// Creates a random/test audio input (useful for development/testing).
    /// </summary>
    public static IAudioInputPort CreateRandom() => new RandomAudioInput();
}
