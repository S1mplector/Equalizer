using System;
using System.Threading;
using System.Threading.Tasks;
using Equalizer.Application.Abstractions;
using Equalizer.Application.Services;
using Equalizer.Application.Models;
using Equalizer.Application.Audio;
using Equalizer.Domain;

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
    private readonly double[] _fluxBassHistory = new double[32];
    private readonly double[] _fluxMidHistory = new double[32];
    private readonly double[] _fluxTrebleHistory = new double[32];
    private int _fluxBassIndex;
    private int _fluxMidIndex;
    private int _fluxTrebleIndex;
    private int _fluxBassCount;
    private int _fluxMidCount;
    private int _fluxTrebleCount;
    private readonly double[] _fluxBassWindow = new double[3];
    private readonly double[] _fluxMidWindow = new double[3];
    private readonly double[] _fluxTrebleWindow = new double[3];
    private int _fluxBassWindowIndex;
    private int _fluxMidWindowIndex;
    private int _fluxTrebleWindowIndex;
    private int _fluxBassWindowCount;
    private int _fluxMidWindowCount;
    private int _fluxTrebleWindowCount;
    private readonly double[] _ibiHistory = new double[32];
    private int _ibiIndex;
    private int _ibiCount;
    private readonly object _frameLock = new();
    private Task<VisualizerFrame>? _inFlight;
    private VisualizerFrame? _lastFrameCache;
    private DateTime _lastFrameAt;
    private double _silenceFade = 1.0; // 1=fully visible, 0=fully faded
    private DateTime _lastBeatAt = DateTime.MinValue;

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
        var now = DateTime.UtcNow;
        var minIntervalMs = 1000.0 / Math.Clamp(settings.TargetFps, 10, 240);
        if (_lastFrameCache != null && (now - _lastFrameAt).TotalMilliseconds < minIntervalMs)
        {
            return _lastFrameCache;
        }

        Task<VisualizerFrame>? task = null;
        lock (_frameLock)
        {
            if (_inFlight != null && !_inFlight.IsCompleted)
            {
                task = _inFlight;
            }
            else
            {
                _inFlight = ComputeFrameInternalAsync(settings, cancellationToken);
                task = _inFlight;
            }
        }

        var vf = await task;
        _lastFrameCache = vf;
        _lastFrameAt = DateTime.UtcNow;
        return vf;
    }

    private async Task<VisualizerFrame> ComputeFrameInternalAsync(EqualizerSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            int minSamples;
            if (settings.Smoothing <= 0.3 && settings.TargetFps >= 120)
            {
                // Low-latency profile: smaller window for faster reaction
                minSamples = 512;
            }
            else if (settings.Smoothing >= 0.7 && settings.TargetFps <= 60)
            {
                // Smooth profile: larger window for more stable spectrum
                minSamples = 2048;
            }
            else
            {
                minSamples = 1024;
            }

            var audioFrame = await _audio.ReadFrameAsync(minSamples, cancellationToken);

        // Silence detection to prevent backlog-looking playback after pause
        double rms = 0;
        var samps = audioFrame.Samples;
        for (int i = 0; i < samps.Length; i++) { double v = samps[i]; rms += v * v; }
        rms = samps.Length > 0 ? Math.Sqrt(rms / samps.Length) : 0;
        bool isSilent = rms < 1e-3; // treat very low-level noise as silence for fading purposes

        double[] mag;
        float[] rawBars;
        if (isSilent)
        {
            mag = _prevMag is { Length: > 0 } ? new double[_prevMag.Length] : Array.Empty<double>();

            if (settings.FadeOnSilenceEnabled && _previous != null && _previous.Length == settings.BarsCount)
            {
                // Keep the last visible shape and let SilenceFade drive the visual fade-out
                rawBars = (float[])_previous.Clone();
            }
            else
            {
                // Legacy behaviour: decay to zero using smoothing
                rawBars = new float[settings.BarsCount];
            }
        }
        else
        {
            // Spectrum and bars
            mag = _processor.ComputeMagnitudes(audioFrame);
            rawBars = _processor.ComputeBarsFromMagnitudes(mag, audioFrame.SampleRate, settings.BarsCount);
        }

        // Apply user-controlled per-band emphasis (bass/treble) to the bar data.
        if (!isSilent && rawBars.Length > 0)
        {
            double bassEmphasis = Math.Clamp(settings.BassEmphasis, 0.0, 2.0);
            double trebleEmphasis = Math.Clamp(settings.TrebleEmphasis, 0.0, 2.0);

            if (Math.Abs(bassEmphasis - 1.0) > 1e-3 || Math.Abs(trebleEmphasis - 1.0) > 1e-3)
            {
                int n = rawBars.Length;
                double bassRegion = 0.45;   // lower ~45% of bars
                double trebleRegion = 0.45; // upper ~45% of bars

                for (int i = 0; i < n; i++)
                {
                    double t = n > 1 ? (double)i / (n - 1) : 0.0; // 0=lowest freq bar, 1=highest

                    double bassWeight = 1.0;
                    if (bassRegion > 0 && t < bassRegion)
                    {
                        double alpha = 1.0 - t / bassRegion; // strongest at very low bars
                        bassWeight = 1.0 + (bassEmphasis - 1.0) * alpha;
                    }

                    double trebleWeight = 1.0;
                    if (trebleRegion > 0 && t > 1.0 - trebleRegion)
                    {
                        double alpha = (t - (1.0 - trebleRegion)) / trebleRegion; // strongest at very high bars
                        trebleWeight = 1.0 + (trebleEmphasis - 1.0) * alpha;
                    }

                    double w = bassWeight * trebleWeight;
                    rawBars[i] = (float)Math.Clamp(rawBars[i] * w, 0.0, 1.0);
                }
            }
        }

        if (_previous == null || _previous.Length != rawBars.Length)
            _previous = new float[rawBars.Length];

        var output = new float[rawBars.Length];
        var smoothing = Math.Clamp(settings.Smoothing, 0.0, 1.0);
        // Asymmetric envelope: allow faster attacks than releases for snappier beats.
        double attackBlend = 0.25 + smoothing * 0.35;  // weight on previous for rising edges
        double releaseBlend = 0.65 + smoothing * 0.25; // heavier smoothing on decay
        if (isSilent && !settings.FadeOnSilenceEnabled)
        {
            // When not using explicit fade, still decay bars a bit faster on silence
            releaseBlend = Math.Min(releaseBlend, 0.35);
        }
        var responsiveness = Math.Clamp(settings.Responsiveness, 0.0, 1.0);
        for (int i = 0; i < rawBars.Length; i++)
        {
            var v = rawBars[i] * (float)(0.5 + responsiveness * 0.5);
            v = Math.Clamp(v, 0f, 1f);
            var prev = _previous[i];
            var blend = v >= prev ? attackBlend : releaseBlend;
            var smoothed = (float)(blend * prev + (1.0 - blend) * v);
            output[i] = smoothed;
            _previous[i] = smoothed;
        }

        // Band energies
        double nyquist = audioFrame.SampleRate / 2.0;
        float band(string name, double f1, double f2)
        {
            if (mag.Length == 0) return 0f;
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

        // Tonal / pitch analysis (YIN-like autocorr with chroma fallback)
        float pitchHue = 0f;
        float pitchStrength = 0f;
        if (!isSilent && mag.Length > 4)
        {
            (pitchHue, pitchStrength) = EstimatePitchHue(audioFrame);
            if (pitchStrength < 0.25f)
            {
                var chroma = new double[12];
                double chromaTotal = 0.0;

                for (int i = 1; i < mag.Length; i++)
                {
                    double f = (double)i / (mag.Length - 1) * nyquist;
                    if (f < 60.0 || f > 5000.0) continue;
                    double m = mag[i];
                    if (m <= 0) continue;

                    double midi = 69.0 + 12.0 * Math.Log(f / 440.0, 2.0);
                    if (double.IsNaN(midi) || double.IsInfinity(midi)) continue;
                    int note = (int)Math.Round(midi);
                    int pc = ((note % 12) + 12) % 12;
                    chroma[pc] += m;
                    chromaTotal += m;
                }

                if (chromaTotal > 1e-6)
                {
                    int bestIndex = 0;
                    double bestValue = 0.0;
                    for (int k = 0; k < 12; k++)
                    {
                        if (chroma[k] > bestValue)
                        {
                            bestValue = chroma[k];
                            bestIndex = k;
                        }
                    }

                    if (bestValue > 0.0)
                    {
                        pitchHue = (float)(bestIndex / 12.0); // 0..1 mapped around the color wheel
                        var chromaStrength = (float)Math.Clamp(bestValue / chromaTotal, 0.0, 1.0);
                        pitchStrength = Math.Max(pitchStrength, chromaStrength * 0.8f);
                    }
                }
            }
        }

        // Spectral flux beat detection (3-band, adaptive + tempo-aware refractory)
        double flux = 0;
        double fluxBass = 0, fluxMid = 0, fluxTreble = 0;
        double energyBass = 0, energyMid = 0, energyTreble = 0;
        if (isSilent)
        {
            ResetBeatState();
        }
        else
        {
            if (_prevMag == null || _prevMag.Length != mag.Length)
            {
                _prevMag = new double[mag.Length];
                Array.Copy(mag, _prevMag, mag.Length);
                flux = 0;
            }
            else
            {
                double magSum = 0;
                for (int i = 0; i < mag.Length; i++)
                {
                    var m = mag[i];
                    magSum += m;
                    var diff = m - _prevMag[i];
                    double freq = (double)i / (mag.Length - 1) * nyquist;
                    if (diff > 0)
                    {
                        flux += diff;
                        if (freq < 250) fluxBass += diff;
                        else if (freq < 2000) fluxMid += diff;
                        else fluxTreble += diff;
                    }

                    if (freq < 250) energyBass += m;
                    else if (freq < 2000) energyMid += m;
                    else energyTreble += m;

                    _prevMag[i] = m;
                }

                // Normalize by overall spectral energy so beats remain visible at lower volumes.
                if (magSum > 1e-9) flux /= magSum;
                if (energyBass > 1e-9) fluxBass /= energyBass;
                if (energyMid > 1e-9) fluxMid /= energyMid;
                if (energyTreble > 1e-9) fluxTreble /= energyTreble;
            }
        }

        // Maintain history for adaptive thresholding
        double thresholdAll = UpdateFluxHistory(_fluxHistory, ref _fluxIndex, ref _fluxCount, flux, 1.2);
        double thresholdBass = UpdateFluxHistory(_fluxBassHistory, ref _fluxBassIndex, ref _fluxBassCount, fluxBass, 1.2);
        double thresholdMid = UpdateFluxHistory(_fluxMidHistory, ref _fluxMidIndex, ref _fluxMidCount, fluxMid, 1.2);
        double thresholdTreble = UpdateFluxHistory(_fluxTrebleHistory, ref _fluxTrebleIndex, ref _fluxTrebleCount, fluxTreble, 1.2);

        // Tiny median filter to ignore single-frame spikes
        double medianBass = Median3(_fluxBassWindow, ref _fluxBassWindowIndex, ref _fluxBassWindowCount, fluxBass);
        double medianMid = Median3(_fluxMidWindow, ref _fluxMidWindowIndex, ref _fluxMidWindowCount, fluxMid);
        double medianTreble = Median3(_fluxTrebleWindow, ref _fluxTrebleWindowIndex, ref _fluxTrebleWindowCount, fluxTreble);

        bool beatBass = !isSilent && _fluxBassCount > 4 && medianBass > thresholdBass && medianBass > 1e-6;
        bool beatMid = !isSilent && _fluxMidCount > 4 && medianMid > thresholdMid && medianMid > 1e-6;
        bool beatTreble = !isSilent && _fluxTrebleCount > 4 && medianTreble > thresholdTreble && medianTreble > 1e-6;

        double ratioBass = thresholdBass > 1e-9 ? medianBass / (thresholdBass + 1e-9) : 0.0;
        double ratioMid = thresholdMid > 1e-9 ? medianMid / (thresholdMid + 1e-9) : 0.0;
        double ratioTreble = thresholdTreble > 1e-9 ? medianTreble / (thresholdTreble + 1e-9) : 0.0;
        double combinedScore = 0.0;
        double weightSum = 0.0;
        if (_fluxBassCount > 4) { combinedScore += 0.5 * ratioBass; weightSum += 0.5; }
        if (_fluxMidCount > 4) { combinedScore += 0.3 * ratioMid; weightSum += 0.3; }
        if (_fluxTrebleCount > 4) { combinedScore += 0.2 * ratioTreble; weightSum += 0.2; }
        combinedScore = weightSum > 0 ? combinedScore / weightSum : 0.0;

        bool isBeatFlag = false;
        float beatStrength = 0f;

        bool candidate = !isSilent && (beatBass || beatMid || beatTreble) && combinedScore > 1.05;
        if (candidate)
        {
            var nowBeat = DateTime.UtcNow;
            var sinceLastMs = (nowBeat - _lastBeatAt).TotalMilliseconds;
            double medianIbi = GetMedianIbi();
            double minGap = 80.0;
            if (medianIbi > 0.0)
            {
                minGap = Math.Clamp(medianIbi * 0.35, 80.0, Math.Max(160.0, medianIbi * 0.9));
            }

            // Tempo-aware refractory window to avoid double-fires
            if (_lastBeatAt == DateTime.MinValue || sinceLastMs >= minGap)
            {
                isBeatFlag = true;
                if (_lastBeatAt != DateTime.MinValue)
                {
                    _ibiHistory[_ibiIndex] = sinceLastMs;
                    _ibiIndex = (_ibiIndex + 1) % _ibiHistory.Length;
                    _ibiCount = Math.Min(_ibiCount + 1, _ibiHistory.Length);
                }
                _lastBeatAt = nowBeat;

                double fluxRatio = thresholdAll > 1e-9 ? flux / (thresholdAll + 1e-9) : 0.0;
                double strength = 0.55 * combinedScore + 0.45 * fluxRatio;
                strength = Math.Max(strength - 1.0, 0.0); // normalize excess above threshold
                beatStrength = (float)Math.Clamp(strength, 0.0, 1.0);
            }
        }

        // Smooth global fade factor for silence-based fading
        float silenceFadeValue = 1f;
        if (settings.FadeOnSilenceEnabled)
        {
            double targetFps = Math.Clamp(settings.TargetFps, 10, 240);
            double frameDt = 1.0 / targetFps;
            double fadeOutSeconds = Math.Clamp(settings.SilenceFadeOutSeconds, 0.05, 10.0);
            double fadeInSeconds = Math.Clamp(settings.SilenceFadeInSeconds, 0.05, 10.0);
            double fadeOutPerFrame = frameDt / fadeOutSeconds;
            double fadeInPerFrame = frameDt / fadeInSeconds;

            if (isSilent)
            {
                _silenceFade = Math.Max(0.0, _silenceFade - fadeOutPerFrame);
            }
            else
            {
                _silenceFade = Math.Min(1.0, _silenceFade + fadeInPerFrame);
            }

            silenceFadeValue = (float)_silenceFade;
        }

        return new VisualizerFrame(output, bass, mid, treble, isBeatFlag, beatStrength, silenceFadeValue, pitchHue, pitchStrength);
        }
        finally
        {
            lock (_frameLock)
            {
                if (_inFlight != null && _inFlight.IsCompleted)
                {
                    _inFlight = null;
                }
            }
        }
    }
 
    private void ResetBeatState()
    {
        if (_prevMag != null) Array.Clear(_prevMag, 0, _prevMag.Length);
        Array.Clear(_fluxHistory, 0, _fluxHistory.Length);
        Array.Clear(_fluxBassHistory, 0, _fluxBassHistory.Length);
        Array.Clear(_fluxMidHistory, 0, _fluxMidHistory.Length);
        Array.Clear(_fluxTrebleHistory, 0, _fluxTrebleHistory.Length);
        Array.Clear(_fluxBassWindow, 0, _fluxBassWindow.Length);
        Array.Clear(_fluxMidWindow, 0, _fluxMidWindow.Length);
        Array.Clear(_fluxTrebleWindow, 0, _fluxTrebleWindow.Length);
        Array.Clear(_ibiHistory, 0, _ibiHistory.Length);
        _fluxIndex = _fluxCount = 0;
        _fluxBassIndex = _fluxMidIndex = _fluxTrebleIndex = 0;
        _fluxBassCount = _fluxMidCount = _fluxTrebleCount = 0;
        _fluxBassWindowIndex = _fluxMidWindowIndex = _fluxTrebleWindowIndex = 0;
        _fluxBassWindowCount = _fluxMidWindowCount = _fluxTrebleWindowCount = 0;
        _ibiIndex = _ibiCount = 0;
        _lastBeatAt = DateTime.MinValue;
    }

    private static double UpdateFluxHistory(double[] history, ref int index, ref int count, double sample, double k)
    {
        history[index] = sample;
        index = (index + 1) % history.Length;
        count = Math.Min(count + 1, history.Length);

        double mean = 0.0;
        for (int i = 0; i < count; i++) mean += history[i];
        mean /= Math.Max(1, count);

        double variance = 0.0;
        for (int i = 0; i < count; i++)
        {
            double d = history[i] - mean;
            variance += d * d;
        }
        variance /= Math.Max(1, count);
        double std = Math.Sqrt(variance);
        return mean + k * std;
    }

    private static double Median3(double[] window, ref int index, ref int count, double sample)
    {
        window[index] = sample;
        index = (index + 1) % window.Length;
        count = Math.Min(count + 1, window.Length);
        if (count == 1) return window[0];
        if (count == 2) return 0.5 * (window[0] + window[1]);
        double a = window[0], b = window[1], c = window[2];
        if (a > b) (a, b) = (b, a);
        if (b > c) (b, c) = (c, b);
        if (a > b) (a, b) = (b, a);
        return b;
    }

    private double GetMedianIbi()
    {
        if (_ibiCount == 0) return 0.0;
        var tmp = new double[_ibiCount];
        for (int i = 0; i < _ibiCount; i++)
        {
            int idx = (_ibiIndex - i - 1 + _ibiHistory.Length) % _ibiHistory.Length;
            tmp[i] = _ibiHistory[idx];
        }
        Array.Sort(tmp);
        int mid = tmp.Length / 2;
        if (tmp.Length % 2 == 1) return tmp[mid];
        return 0.5 * (tmp[mid - 1] + tmp[mid]);
    }

    private static (float hue, float strength) EstimatePitchHue(AudioFrame frame)
    {
        var samples = frame.Samples;
        int sampleRate = frame.SampleRate;
        if (sampleRate <= 0 || samples.Length < 128) return (0f, 0f);

        int maxLen = Math.Min(samples.Length, 4096);
        double energy = 0.0;
        for (int i = 0; i < maxLen; i++) energy += samples[i] * samples[i];
        if (energy < 1e-7) return (0f, 0f);

        int minLag = Math.Max(1, sampleRate / 800); // ~800 Hz upper bound
        int maxLag = Math.Min(maxLen - 1, sampleRate / 60); // ~60 Hz lower bound
        if (minLag >= maxLag) return (0f, 0f);

        double bestCorr = 0.0;
        int bestLag = 0;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < maxLen - lag; i++)
            {
                double a = samples[i];
                double b = samples[i + lag];
                sum += a * b;
                normA += a * a;
                normB += b * b;
            }
            double denom = Math.Sqrt(normA * normB) + 1e-9;
            double corr = sum / denom;
            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestLag = lag;
            }
        }

        if (bestLag == 0) return (0f, 0f);
        double pitchHz = (double)sampleRate / bestLag;
        double midi = 69.0 + 12.0 * Math.Log(pitchHz / 440.0, 2.0);
        if (double.IsNaN(midi) || double.IsInfinity(midi)) return (0f, 0f);
        float hue = (float)(((midi % 12.0) + 12.0) % 12.0 / 12.0);
        float strength = (float)Math.Clamp((bestCorr - 0.5) / 0.5, 0.0, 1.0);
        return (hue, strength);
    }
}
