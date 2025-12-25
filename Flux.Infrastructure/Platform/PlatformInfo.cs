using System;
using System.IO;
using System.Runtime.InteropServices;
using Flux.Application.Abstractions;

namespace Flux.Infrastructure.Platform;

public sealed class PlatformInfo : IPlatformInfo
{
    public PlatformType Platform { get; }
    public bool IsWindows => Platform == PlatformType.Windows;
    public bool IsMacOS => Platform == PlatformType.MacOS;
    public bool IsLinux => Platform == PlatformType.Linux;

    public string AppDataDirectory { get; }

    public PlatformInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Platform = PlatformType.Windows;
            AppDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Flux");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Platform = PlatformType.MacOS;
            AppDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Flux");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Platform = PlatformType.Linux;
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            AppDataDirectory = !string.IsNullOrEmpty(xdgConfig)
                ? Path.Combine(xdgConfig, "Flux")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Flux");
        }
        else
        {
            Platform = PlatformType.Unknown;
            AppDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".flux");
        }

        // Ensure directory exists
        Directory.CreateDirectory(AppDataDirectory);
    }
}
