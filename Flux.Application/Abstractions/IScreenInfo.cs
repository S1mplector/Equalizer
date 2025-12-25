using System.Collections.Generic;

namespace Flux.Application.Abstractions;

/// <summary>
/// Represents information about a display/monitor.
/// </summary>
public record ScreenInfo(
    string Id,
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary,
    double ScaleFactor
);

/// <summary>
/// Platform-specific screen/display enumeration.
/// </summary>
public interface IScreenProvider
{
    /// <summary>
    /// Gets all available screens/monitors.
    /// </summary>
    IReadOnlyList<ScreenInfo> GetScreens();

    /// <summary>
    /// Gets the primary screen.
    /// </summary>
    ScreenInfo? GetPrimaryScreen();

    /// <summary>
    /// Gets the virtual screen bounds (union of all monitors).
    /// </summary>
    (int X, int Y, int Width, int Height) GetVirtualScreenBounds();
}
