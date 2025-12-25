using System.Collections.Generic;
using System.Linq;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Platform;
using Flux.Application.Abstractions;

namespace Flux.Avalonia.Services;

public class AvaloniaScreenProvider : IScreenProvider
{
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var screens = GetAvaloniaScreens();
        return screens.Select((s, i) => new ScreenInfo(
            Id: i.ToString(),
            Name: $"Display {i + 1}",
            X: (int)s.Bounds.X,
            Y: (int)s.Bounds.Y,
            Width: (int)s.Bounds.Width,
            Height: (int)s.Bounds.Height,
            IsPrimary: s.IsPrimary,
            ScaleFactor: s.Scaling
        )).ToList();
    }

    public ScreenInfo? GetPrimaryScreen()
    {
        var screens = GetScreens();
        return screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault();
    }

    public (int X, int Y, int Width, int Height) GetVirtualScreenBounds()
    {
        var screens = GetAvaloniaScreens();
        if (!screens.Any())
            return (0, 0, 1920, 1080);

        int minX = screens.Min(s => (int)s.Bounds.X);
        int minY = screens.Min(s => (int)s.Bounds.Y);
        int maxX = screens.Max(s => (int)(s.Bounds.X + s.Bounds.Width));
        int maxY = screens.Max(s => (int)(s.Bounds.Y + s.Bounds.Height));

        return (minX, minY, maxX - minX, maxY - minY);
    }

    private static IReadOnlyList<global::Avalonia.Platform.Screen> GetAvaloniaScreens()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.FirstOrDefault();
            if (window != null)
            {
                return window.Screens.All;
            }
        }
        
        return new List<Screen>();
    }
}
