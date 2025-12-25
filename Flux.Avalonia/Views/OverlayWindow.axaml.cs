using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Flux.Application.Abstractions;
using Flux.Application.Services;
using Flux.Domain;

namespace Flux.Avalonia.Views;

public partial class OverlayWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IFluxService? _fluxService;
    private readonly ISettingsPort? _settingsPort;
    private readonly DispatcherTimer _renderTimer;
    private float[]? _currentSpectrum;
    private FluxSettings? _settings;

    public OverlayWindow() : this(null!)
    {
        // Design-time constructor
    }

    public OverlayWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();

        if (services != null)
        {
            _fluxService = services.GetService<IFluxService>();
            _settingsPort = services.GetService<ISettingsPort>();
            _settings = _settingsPort?.GetAsync().GetAwaiter().GetResult();

            if (_fluxService != null)
            {
                _fluxService.SpectrumUpdated += OnSpectrumUpdated;
            }
        }

        // Set up render timer for smooth animation
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _renderTimer.Tick += (_, _) => InvalidateVisual();
        _renderTimer.Start();

        // Platform-specific window setup
        SetupPlatformWindow();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupPlatformWindow()
    {
        // Make window click-through by default
        // Platform-specific implementations will handle this
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetupWindowsOverlay();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SetupMacOSOverlay();
        }
    }

    private void SetupWindowsOverlay()
    {
        // Windows-specific: Set extended window styles after window is shown
        this.Opened += (_, _) =>
        {
            var platformHandle = GetNativePlatformHandle();
            if (platformHandle != IntPtr.Zero)
            {
                WindowsInterop.ApplyOverlayStyles(platformHandle);
                WindowsInterop.SetClickThrough(platformHandle, true);
            }
        };
    }

    private void SetupMacOSOverlay()
    {
        // macOS-specific: Configure NSWindow properties
        this.Opened += (_, _) =>
        {
            var platformHandle = GetNativePlatformHandle();
            if (platformHandle != IntPtr.Zero)
            {
                MacOSInterop.ApplyOverlayStyles(platformHandle);
            }
        };
    }

    public void SetClickThrough(bool clickThrough)
    {
        var handle = GetNativePlatformHandle();
        if (handle == IntPtr.Zero) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsInterop.SetClickThrough(handle, clickThrough);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSInterop.SetClickThrough(handle, clickThrough);
        }
    }

    private IntPtr GetNativePlatformHandle()
    {
        try
        {
            var handle = base.TryGetPlatformHandle();
            return handle?.Handle ?? IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private void OnSpectrumUpdated(float[] spectrum)
    {
        _currentSpectrum = spectrum;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_currentSpectrum == null || _settings == null) return;

        // Use Avalonia's drawing context to render the spectrum
        RenderSpectrum(context, _currentSpectrum, _settings);
    }

    private void RenderSpectrum(DrawingContext context, float[] spectrum, FluxSettings settings)
    {
        var bounds = this.Bounds;
        int barCount = settings.BarsCount;
        float barWidth = (float)bounds.Width / barCount;
        float maxHeight = (float)bounds.Height * 0.8f;

        var brush = new SolidColorBrush(Color.FromArgb(
            255,
            settings.Color.R,
            settings.Color.G,
            settings.Color.B));

        for (int i = 0; i < Math.Min(spectrum.Length, barCount); i++)
        {
            float barHeight = spectrum[i] * maxHeight;
            float x = i * barWidth;
            float y = (float)bounds.Height - barHeight;

            var rect = new Rect(x, y, barWidth - 2, barHeight);
            context.FillRectangle(brush, rect);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderTimer.Stop();
        if (_fluxService != null)
        {
            _fluxService.SpectrumUpdated -= OnSpectrumUpdated;
        }
        base.OnClosed(e);
    }
}

// Platform interop helpers
internal static class WindowsInterop
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void ApplyOverlayStyles(IntPtr hWnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
    }

    public static void SetClickThrough(IntPtr hWnd, bool clickThrough)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        if (clickThrough)
            exStyle |= WS_EX_TRANSPARENT;
        else
            exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
    }
}

internal static class MacOSInterop
{
    // NSWindowCollectionBehavior flags
    private const long NSWindowCollectionBehaviorCanJoinAllSpaces = 1 << 0;
    private const long NSWindowCollectionBehaviorStationary = 1 << 4;
    private const long NSWindowCollectionBehaviorIgnoresCycle = 1 << 6;

    public static void ApplyOverlayStyles(IntPtr nsWindowPtr)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        // These would require Objective-C runtime calls
        // For now, Avalonia handles basic transparency
        // Full implementation would use:
        // - [NSWindow setLevel:] for window level
        // - [NSWindow setCollectionBehavior:] for space behavior
        // - [NSWindow setIgnoresMouseEvents:] for click-through
    }

    public static void SetClickThrough(IntPtr nsWindowPtr, bool clickThrough)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        // Would call [NSWindow setIgnoresMouseEvents:clickThrough]
        // Requires Objective-C runtime interop
    }
}
