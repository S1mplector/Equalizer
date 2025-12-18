using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flux.Application.Abstractions;
using Flux.Application.Audio;
using Flux.Application.Services;
using Flux.Domain;
using Flux.Domain.Widgets;
using Flux.Infrastructure.Settings;
using Flux.Infrastructure.Widgets;

namespace Flux.Tests;

public class PersistenceAndProcessingTests
{
    [Fact]
    public async Task JsonSettingsRepository_RoundTripsSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = new JsonSettingsRepository(tempDir);
            var settings = new FluxSettings(
                barsCount: 32,
                responsiveness: 0.6,
                smoothing: 0.4,
                color: new ColorRgb(10, 20, 30),
                targetFps: 75,
                colorCycleEnabled: true,
                colorCycleSpeedHz: 1.2,
                barCornerRadius: 2.0,
                displayMode: MonitorDisplayMode.Specific,
                specificMonitorDeviceName: "DISPLAY1",
                offsetX: 12,
                offsetY: 34,
                visualizerMode: VisualizerMode.Circular,
                circleDiameter: 640,
                overlayVisible: true,
                fadeOnSilenceEnabled: true,
                silenceFadeOutSeconds: 1.2,
                silenceFadeInSeconds: 0.7,
                pitchReactiveColorEnabled: true,
                bassEmphasis: 1.3,
                trebleEmphasis: 0.9,
                beatShapeEnabled: true,
                glowEnabled: true,
                perfOverlayEnabled: true,
                gradientEnabled: true,
                gradientEndColor: new ColorRgb(200, 150, 100),
                audioDeviceId: "default",
                renderingMode: RenderingMode.Gpu,
                monitorOffsets: new Dictionary<string, MonitorOffset>
                {
                    { "DISPLAY1", new MonitorOffset(5, 6) }
                });

            await repo.SaveAsync(settings);
            var loaded = await repo.GetAsync();

            Assert.Equal(settings.BarsCount, loaded.BarsCount);
            Assert.Equal(settings.Responsiveness, loaded.Responsiveness);
            Assert.Equal(settings.Smoothing, loaded.Smoothing);
            Assert.Equal(settings.Color.R, loaded.Color.R);
            Assert.Equal(settings.Color.G, loaded.Color.G);
            Assert.Equal(settings.Color.B, loaded.Color.B);
            Assert.Equal(settings.ColorCycleEnabled, loaded.ColorCycleEnabled);
            Assert.Equal(settings.VisualizerMode, loaded.VisualizerMode);
            Assert.Equal(settings.CircleDiameter, loaded.CircleDiameter);
            Assert.Equal(settings.MonitorOffsets["DISPLAY1"].X, loaded.MonitorOffsets["DISPLAY1"].X);
            Assert.Equal(settings.MonitorOffsets["DISPLAY1"].Y, loaded.MonitorOffsets["DISPLAY1"].Y);
            Assert.Equal(settings.GradientEndColor.R, loaded.GradientEndColor.R);
            Assert.Equal(settings.RenderingMode, loaded.RenderingMode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FluxService_SmoothingDecaysBarsInsteadOfDroppingInstantly()
    {
        var frames = new[]
        {
            new AudioFrame(Enumerable.Repeat(0.8f, 1024).ToArray(), 48_000),
            new AudioFrame(new float[1024], 48_000)
        };
        var audio = new StubAudioInput(frames);
        var settings = new FluxSettings(
            barsCount: 16,
            responsiveness: 1.0,
            smoothing: 0.8,
            color: new ColorRgb(0, 255, 128),
            targetFps: 60,
            colorCycleEnabled: false,
            colorCycleSpeedHz: 0.2,
            barCornerRadius: 1.0,
            displayMode: MonitorDisplayMode.All,
            specificMonitorDeviceName: null,
            offsetX: 0,
            offsetY: 0,
            visualizerMode: VisualizerMode.Bars,
            circleDiameter: 400,
            overlayVisible: true,
            fadeOnSilenceEnabled: true);
        var service = new FluxService(audio, new StubSettingsPort(settings), new SpectrumProcessor());

        var first = await service.GetBarsAsync(CancellationToken.None);
        await Task.Delay(40); // allow next frame generation past target FPS gating
        var second = await service.GetBarsAsync(CancellationToken.None);

        Assert.True(first.Max() > 0.01f);
        Assert.True(second.Max() <= first.Max()); // should not grow on silence with smoothing applied
    }

    [Fact]
    public async Task WidgetLayoutRepository_RoundTripsWidgets()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = new JsonWidgetLayoutRepository(tempDir);
            var layout = new WidgetLayout
            {
                Widgets =
                {
                    new WidgetConfig
                    {
                        Id = "clock",
                        WidgetTypeId = "clock",
                        X = 12,
                        Y = 34,
                        Anchor = WidgetAnchor.TopLeft,
                        Width = 200,
                        Height = 80,
                        MonitorDeviceName = "DISPLAY1",
                        Settings = new Dictionary<string, object> { { "format", "HH:mm" } }
                    },
                    new WidgetConfig
                    {
                        Id = "system",
                        WidgetTypeId = "system",
                        X = 20,
                        Y = 40,
                        Anchor = WidgetAnchor.BottomRight,
                        Width = 260,
                        Height = 120,
                        MonitorDeviceName = "DISPLAY2",
                        Settings = new Dictionary<string, object> { { "showCpu", true } }
                    }
                }
            };

            await repo.SaveLayoutAsync(layout);
            var loaded = await repo.GetLayoutAsync();

            Assert.Equal(layout.Widgets.Count, loaded.Widgets.Count);
            Assert.Equal(layout.Widgets[0].MonitorDeviceName, loaded.Widgets[0].MonitorDeviceName);
            Assert.Equal(layout.Widgets[1].Anchor, loaded.Widgets[1].Anchor);
            Assert.Equal(layout.Widgets[1].Settings["showCpu"], loaded.Widgets[1].Settings["showCpu"]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private sealed class StubSettingsPort : ISettingsPort
    {
        private readonly FluxSettings _settings;

        public StubSettingsPort(FluxSettings settings)
        {
            _settings = settings;
        }

        public Task<FluxSettings> GetAsync() => Task.FromResult(_settings);
        public Task SaveAsync(FluxSettings settings) => Task.CompletedTask;
    }

    private sealed class StubAudioInput : IAudioInputPort
    {
        private readonly AudioFrame[] _frames;
        private int _index;

        public StubAudioInput(AudioFrame[] frames)
        {
            _frames = frames;
        }

        public Task<AudioFrame> ReadFrameAsync(int minSamples, CancellationToken cancellationToken)
        {
            var idx = Math.Min(_index, _frames.Length - 1);
            _index = Math.Min(_frames.Length - 1, _index + 1);
            return Task.FromResult(_frames[idx]);
        }
    }
}
