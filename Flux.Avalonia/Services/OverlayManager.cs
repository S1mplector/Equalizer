using System;
using System.Threading.Tasks;
using global::Avalonia.Threading;
using Flux.Application.Abstractions;
using Flux.Avalonia.Views;

namespace Flux.Avalonia.Services;

public class OverlayManager : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly IScreenProvider _screenProvider;
    private readonly ISettingsPort _settingsPort;
    private OverlayWindow? _overlayWindow;
    private bool _isVisible;
    private bool _clickThrough = true;
    private bool _alwaysOnTop;

    public bool IsVisible => _isVisible;
    public bool ClickThrough => _clickThrough;
    public bool AlwaysOnTop => _alwaysOnTop;

    public OverlayManager(
        IServiceProvider services,
        IScreenProvider screenProvider,
        ISettingsPort settingsPort)
    {
        _services = services;
        _screenProvider = screenProvider;
        _settingsPort = settingsPort;
    }

    public async Task InitializeAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var bounds = _screenProvider.GetVirtualScreenBounds();
            _overlayWindow = new OverlayWindow(_services)
            {
                Position = new global::Avalonia.PixelPoint(bounds.X, bounds.Y),
                Width = bounds.Width,
                Height = bounds.Height
            };
        });
    }

    public async Task ShowAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.Show();
            _isVisible = true;
        });
    }

    public async Task HideAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.Hide();
            _isVisible = false;
        });
    }

    public async Task ToggleAsync()
    {
        if (_isVisible)
            await HideAsync();
        else
            await ShowAsync();
    }

    public async Task ToggleClickThroughAsync()
    {
        _clickThrough = !_clickThrough;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.SetClickThrough(_clickThrough);
        });
    }

    public async Task ToggleAlwaysOnTopAsync()
    {
        _alwaysOnTop = !_alwaysOnTop;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Topmost = _alwaysOnTop;
            }
        });
    }

    public async Task ResetPositionAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var bounds = _screenProvider.GetVirtualScreenBounds();
            if (_overlayWindow != null)
            {
                _overlayWindow.Position = new global::Avalonia.PixelPoint(bounds.X, bounds.Y);
                _overlayWindow.Width = bounds.Width;
                _overlayWindow.Height = bounds.Height;
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _overlayWindow?.Close();
            _overlayWindow = null;
        });
    }
}
