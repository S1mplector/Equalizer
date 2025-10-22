using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Equalizer.Application.Abstractions;
using Equalizer.Domain;

namespace Equalizer.Presentation.Overlay;

public partial class OverlayWindow : Window
{
    private readonly IEqualizerService _service;
    private readonly ISettingsPort _settings;
    private readonly List<System.Windows.Shapes.Rectangle> _bars = new();
    private readonly DispatcherTimer _timer = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _rendering;
    private SolidColorBrush _barBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 128));
    private DateTime _lastFrame = DateTime.MinValue;
    private double _cyclePhase;
    private double _beatPulse;

    public OverlayWindow(IEqualizerService service, ISettingsPort settings)
    {
        _service = service;
        _settings = settings;
        InitializeComponent();

        _timer.Interval = TimeSpan.FromMilliseconds(33); // ~30 FPS
        _timer.Tick += async (_, __) => await RenderAsync();
        Loaded += (_, __) => { _timer.Start(); System.Windows.Media.CompositionTarget.Rendering += OnRendering; };
        Unloaded += (_, __) => { _timer.Stop(); System.Windows.Media.CompositionTarget.Rendering -= OnRendering; };
        Closed += (_, __) => _cts.Cancel();
        SizeChanged += (_, __) => LayoutBars();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _ = RenderAsync();
    }

    private async Task RenderAsync()
    {
        if (_rendering) return;
        _rendering = true;
        try
        {
            var s = await _settings.GetAsync();

            var now = DateTime.UtcNow;
            var minIntervalMs = 1000.0 / Math.Clamp(s.TargetFps, 10, 240);
            if (_lastFrame != DateTime.MinValue)
            {
                var dt = (now - _lastFrame).TotalMilliseconds;
                if (dt < minIntervalMs) return;
            }
            _lastFrame = now;

            var vf = await _service.GetVisualizerFrameAsync(_cts.Token);
            var data = vf.Bars;
            EnsureBars(data.Length);

            var width = BarsCanvas.ActualWidth;
            var height = BarsCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            var spacing = 2.0;
            var barWidth = Math.Max(1.0, (width - spacing * (data.Length - 1)) / data.Length);

            var color = s.Color;
            if (s.ColorCycleEnabled)
            {
                _cyclePhase += s.ColorCycleSpeedHz * (minIntervalMs / 1000.0) * 360.0;
                _cyclePhase %= 360.0;
                var rgb = HsvToRgb(_cyclePhase, 1.0, 1.0);
                color = new ColorRgb((byte)rgb.r, (byte)rgb.g, (byte)rgb.b);
            }
            // Beat pulse
            if (vf.IsBeat) _beatPulse = Math.Min(1.0, _beatPulse + vf.BeatStrength * 0.8);
            _beatPulse *= 0.9; // decay

            var baseColor = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
            var pulsed = LerpColor(baseColor, System.Windows.Media.Colors.White, (float)(0.35 * _beatPulse));
            if (_barBrush.Color != pulsed) _barBrush.Color = pulsed;

            for (int i = 0; i < data.Length; i++)
            {
                // Slight bass/treble emphasis and beat pulse scaling
                var scale = 1.0 + 0.12 * vf.Bass + 0.06 * vf.Treble + 0.1 * _beatPulse;
                var h = Math.Max(1.0, data[i] * height * scale);
                var left = i * (barWidth + spacing);
                var top = height - h;
                var rect = _bars[i];
                rect.Width = barWidth;
                rect.Height = h;
                rect.RadiusX = s.BarCornerRadius;
                rect.RadiusY = s.BarCornerRadius;
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
            }
        }
        finally
        {
            _rendering = false;
        }
    }

    private void EnsureBars(int count)
    {
        if (_bars.Count == count) return;
        BarsCanvas.Children.Clear();
        _bars.Clear();

        for (int i = 0; i < count; i++)
        {
            var r = new System.Windows.Shapes.Rectangle
            {
                Fill = _barBrush,
                RadiusX = 1,
                RadiusY = 1
            };
            _bars.Add(r);
            BarsCanvas.Children.Add(r);
        }
        LayoutBars();
    }

    private void LayoutBars()
    {
        if (_bars.Count == 0) return;
        var width = BarsCanvas.ActualWidth;
        var height = BarsCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var spacing = 2.0;
        var barWidth = Math.Max(1.0, (width - spacing * (_bars.Count - 1)) / _bars.Count);
        for (int i = 0; i < _bars.Count; i++)
        {
            var left = i * (barWidth + spacing);
            var rect = _bars[i];
            rect.Width = barWidth;
            Canvas.SetLeft(rect, left);
        }
    }

    private static (int r, int g, int b) HsvToRgb(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        int i = (int)Math.Floor(h / 60.0) % 6;
        double f = h / 60.0 - Math.Floor(h / 60.0);
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);
        double r = 0, g = 0, b = 0;
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }
        return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
}
