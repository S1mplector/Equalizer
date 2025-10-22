using System;
using System.Threading;
using System.Threading.Tasks;
using Equalizer.Application.Abstractions;
using Equalizer.Application.Services;
using Equalizer.Application.Models;

namespace Equalizer.Application.Services;

public sealed class EqualizerService : IEqualizerService
{
    private readonly IAudioInputPort _audio;
    private readonly ISettingsPort _settings;
    private readonly SpectrumProcessor _processor;
    private float[]? _previous;
    private double[]? _prevMag;
    private readonly double[] _fluxHistory = new double[64];
    private int _fluxIndex;
    private int _fluxCount;

    public EqualizerService(IAudioInputPort audio, ISettingsPort settings, SpectrumProcessor processor)
    {
        _audio = audio;
        _settings = settings;
        _processor = processor;
    }

    public async Task<float[]> GetBarsAsync(CancellationToken cancellationToken)
    {
        var vf = await GetVisualizerFrameAsync(cancellationToken);
        return vf.Bars;
    }

    public async Task<VisualizerFrame> GetVisualizerFrameAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync();
        var audioFrame = await _audio.ReadFrameAsync(minSamples: 1024, cancellationToken);

        // Spectrum and bars
        var mag = _processor.ComputeMagnitudes(audioFrame);
        var rawBars = _processor.ComputeBarsFromMagnitudes(mag, audioFrame.SampleRate, settings.BarsCount);

        if (_previous == null || _previous.Length != rawBars.Length)
            _previous = new float[rawBars.Length];

        var output = new float[rawBars.Length];
        var smoothing = Math.Clamp(settings.Smoothing, 0.0, 1.0);
        var responsiveness = Math.Clamp(settings.Responsiveness, 0.0, 1.0);
        for (int i = 0; i < rawBars.Length; i++)
        {
            var v = rawBars[i] * (float)(0.5 + responsiveness * 0.5);
            v = Math.Clamp(v, 0f, 1f);
            var smoothed = (float)(smoothing * _previous[i] + (1.0 - smoothing) * v);
            output[i] = smoothed;
            _previous[i] = smoothed;
        }

        // Band energies
        double nyquist = audioFrame.SampleRate / 2.0;
        float band(string name, double f1, double f2)
        {
            int i1 = (int)Math.Clamp(Math.Round(f1 / nyquist * (mag.Length - 1)), 1, mag.Length - 1);
            int i2 = (int)Math.Clamp(Math.Round(f2 / nyquist * (mag.Length - 1)), i1 + 1, mag.Length - 1);
            double sum = 0; int cnt = 0;
            for (int i = i1; i <= i2; i++) { sum += mag[i]; cnt++; }
            double avg = cnt > 0 ? sum / cnt : 0;
            return (float)Math.Clamp(Math.Sqrt(avg) * 2.0, 0.0, 1.0);
        }
        var bass = band("bass", 20, 250);
        var mid = band("mid", 250, 2000);
        var treble = band("treble", 2000, 16000);

        // Spectral flux beat detection
        double flux = 0;
        if (_prevMag == null || _prevMag.Length != mag.Length) _prevMag = new double[mag.Length];
        for (int i = 0; i < mag.Length; i++)
        {
            var diff = mag[i] - _prevMag[i];
            if (diff > 0) flux += diff;
        }
        Array.Copy(mag, _prevMag, mag.Length);

        // Maintain history
        _fluxHistory[_fluxIndex] = flux;
        _fluxIndex = (_fluxIndex + 1) % _fluxHistory.Length;
        _fluxCount = Math.Min(_fluxCount + 1, _fluxHistory.Length);

        double mean = 0; for (int i = 0; i < _fluxCount; i++) mean += _fluxHistory[i]; mean /= Math.Max(1, _fluxCount);
        double var = 0; for (int i = 0; i < _fluxCount; i++) { var += ( _fluxHistory[i] - mean) * (_fluxHistory[i] - mean); }
        var /= Math.Max(1, _fluxCount);
        double std = Math.Sqrt(var);
        double threshold = mean + 1.5 * std;
        bool isBeat = flux > threshold && flux > 1e-6;
        float beatStrength = isBeat ? (float)Math.Clamp((flux - threshold) / Math.Max(threshold, 1e-6), 0.0, 1.0) : 0f;

        return new VisualizerFrame(output, bass, mid, treble, isBeat, beatStrength);
    }
}
