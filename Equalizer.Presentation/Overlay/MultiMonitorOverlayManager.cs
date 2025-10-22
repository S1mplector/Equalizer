using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Equalizer.Presentation.Interop;
using Forms = System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Equalizer.Presentation.Overlay;

public sealed class MultiMonitorOverlayManager : IOverlayManager
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, OverlayWindow> _windows = new();
    private bool _clickThrough;
    private bool _alwaysOnTop;

    public MultiMonitorOverlayManager(IServiceProvider services)
    {
        _services = services;
    }

    public bool IsVisible => _windows.Values.Any(w => w.IsVisible);
    public bool ClickThrough => _clickThrough;
    public bool AlwaysOnTop => _alwaysOnTop;

    public async Task ShowAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            EnsureWindows();
            foreach (var win in _windows.Values)
            {
                if (!win.IsVisible) win.Show();
                ApplyStyles(win);
            }
        });
    }

    public async Task HideAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var win in _windows.Values)
            {
                if (win.IsVisible) win.Hide();
            }
        });
    }

    public Task ToggleAsync() => IsVisible ? HideAsync() : ShowAsync();

    public Task SetClickThroughAsync(bool value)
    {
        _clickThrough = value;
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var win in _windows.Values)
            {
                WindowStyles.ApplyOverlayExtendedStyles(win, _clickThrough);
            }
        }).Task;
    }

    public Task ToggleClickThroughAsync() => SetClickThroughAsync(!_clickThrough);

    public Task SetAlwaysOnTopAsync(bool value)
    {
        _alwaysOnTop = value;
        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var win in _windows.Values)
            {
                WindowStyles.SetTopMost(win, _alwaysOnTop);
                if (!_alwaysOnTop)
                {
                    WindowStyles.SendToBottom(win);
                }
            }
        }).Task;
    }

    public Task ToggleAlwaysOnTopAsync() => SetAlwaysOnTopAsync(!_alwaysOnTop);

    private void EnsureWindows()
    {
        var screens = Forms.Screen.AllScreens;
        var keys = _windows.Keys.ToHashSet();
        var existing = new HashSet<string>();

        foreach (var screen in screens)
        {
            string key = screen.DeviceName;
            existing.Add(key);
            if (!_windows.ContainsKey(key))
            {
                var win = _services.GetRequiredService<OverlayWindow>();
                ConfigureForScreen(win, screen);
                _windows[key] = win;
            }
            else
            {
                ConfigureForScreen(_windows[key], screen);
            }
        }

        // Remove windows for screens no longer present
        foreach (var k in keys.Except(existing).ToList())
        {
            if (_windows.TryGetValue(k, out var w))
            {
                w.Close();
                _windows.Remove(k);
            }
        }
    }

    private void ConfigureForScreen(OverlayWindow window, Forms.Screen screen)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var bounds = screen.Bounds; // pixels
        // Convert to WPF DPI-independent units (assume 96 DPI, WPF will scale per monitor automatically)
        double left = bounds.Left;
        double top = bounds.Top;
        double width = bounds.Width;
        double height = bounds.Height;
        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;
        WindowStyles.ApplyOverlayExtendedStyles(window, _clickThrough);
        WindowStyles.SetTopMost(window, _alwaysOnTop);
        if (!_alwaysOnTop) WindowStyles.SendToBottom(window);
    }

    private void ApplyStyles(OverlayWindow window)
    {
        WindowStyles.ApplyOverlayExtendedStyles(window, _clickThrough);
        WindowStyles.SetTopMost(window, _alwaysOnTop);
        if (!_alwaysOnTop) WindowStyles.SendToBottom(window);
    }
}
