using System;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using Equalizer.Application.Audio;

namespace Equalizer.Application.Services;

public sealed class SpectrumProcessor
{
    private readonly object _lock = new();
    private Complex[]? _complex;
    private double[]? _mag;
    private double[]? _hann;
    private int _n;
    private FilterbankCache? _filterbankCache;

    public float[] ComputeBars(AudioFrame frame, int bars)
    {
        if (bars <= 0) return Array.Empty<float>();
        var samples = frame.Samples;
        if (samples.Length == 0) return new float[bars];

        var mag = ComputeMagnitudes(frame);
        return ComputeBarsFromMagnitudes(mag, frame.SampleRate, bars);
    }

    public double[] ComputeMagnitudes(AudioFrame frame)
    {
        var samples = frame.Samples;
        int n = NextPowerOfTwo(Math.Min(samples.Length, 4096));
        lock (_lock)
        {
            if (n != _n || _complex == null || _hann == null || _mag == null)
            {
                _n = n;
                _complex = new Complex[n];
                _hann = new double[n];
                for (int i = 0; i < n; i++)
                {
                    _hann[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1))); // Hann window
                }
                _mag = new double[n / 2];
            }

            var complex = _complex!;
            var hann = _hann!;
            for (int i = 0; i < n; i++)
            {
                double s = i < samples.Length ? samples[i] : 0.0;
                complex[i] = new Complex(s * hann[i], 0);
            }

            Fourier.Forward(complex, FourierOptions.Matlab);
            var mag = _mag!;
            for (int i = 0; i < mag.Length; i++)
            {
                var c = complex[i];
                mag[i] = (2.0 / n) * c.Magnitude;
            }
            // Return a copy to keep the shared buffer thread-safe for concurrent callers.
            return mag.ToArray();
        }
    }

    public float[] ComputeBarsFromMagnitudes(double[] mag, int sampleRate, int bars)
    {
        var result = new float[bars];
        double nyquist = sampleRate / 2.0;
        double fMin = 50.0;
        double fMax = Math.Min(18000.0, nyquist);
        if (fMax <= fMin) return result;

        // Reuse precomputed mel filterbank for this bars/sampleRate/magLength combination.
        var filterbank = GetOrBuildFilterbank(bars, sampleRate, mag.Length, fMin, fMax, nyquist);

        for (int b = 0; b < bars; b++)
        {
            var filter = filterbank.Filters[b];
            var weights = filter.Weights;
            double energy = 0.0;
            for (int i = 0; i < weights.Length; i++)
            {
                int bin = filter.Start + i;
                energy += mag[bin] * weights[i];
            }

            // Perceptual compression: log-like curve keeps low levels visible without
            // flattening strong transients.
            double logEnergy = Math.Log10(1.0 + energy * 9.0);
            double scaled = logEnergy * 1.1;
            result[b] = (float)Math.Clamp(scaled, 0.0, 1.0);
        }
        return result;
    }

    private static int NextPowerOfTwo(int x)
    {
        int p = 1;
        while (p < x) p <<= 1;
        return p;
    }

    private FilterbankCache GetOrBuildFilterbank(int bars, int sampleRate, int magLength, double fMin, double fMax, double nyquist)
    {
        lock (_lock)
        {
            var key = new FilterbankCacheKey(bars, sampleRate, magLength);
            var cache = _filterbankCache;
            if (cache.HasValue && cache.Value.Key.Equals(key))
            {
                return cache.Value;
            }

            var filters = new Filter[bars];

            double melMin = HzToMel(fMin);
            double melMax = HzToMel(fMax);
            double melStep = (melMax - melMin) / (bars + 1);

            for (int b = 0; b < bars; b++)
            {
                double melL = melMin + b * melStep;
                double melC = melMin + (b + 1) * melStep;
                double melR = melMin + (b + 2) * melStep;

                double fL = MelToHz(melL);
                double fC = MelToHz(melC);
                double fR = MelToHz(melR);

                int iStart = (int)Math.Clamp(Math.Floor(fL / nyquist * (magLength - 1)), 0, magLength - 1);
                int iEnd = (int)Math.Clamp(Math.Ceiling(fR / nyquist * (magLength - 1)), iStart + 1, magLength - 1);
                int len = iEnd - iStart + 1;
                var weights = new double[len];
                double sum = 0.0;
                for (int i = 0; i < len; i++)
                {
                    int bin = iStart + i;
                    double freq = (double)bin / (magLength - 1) * nyquist;
                    double w;
                    if (freq <= fL || freq >= fR)
                    {
                        w = 0.0;
                    }
                    else if (freq <= fC)
                    {
                        w = (freq - fL) / Math.Max(1e-6, fC - fL);
                    }
                    else
                    {
                        w = (fR - freq) / Math.Max(1e-6, fR - fC);
                    }
                    w = Math.Max(0.0, w);
                    weights[i] = w;
                    sum += w;
                }

                if (sum > 1e-9)
                {
                    for (int i = 0; i < len; i++) weights[i] /= sum;
                }

                filters[b] = new Filter(iStart, iEnd, weights);
            }

            var built = new FilterbankCache(key, filters);
            _filterbankCache = built;
            return built;
        }
    }

    private static double HzToMel(double hz) => 2595.0 * Math.Log10(1.0 + hz / 700.0);
    private static double MelToHz(double mel) => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);

    private readonly struct FilterbankCacheKey : IEquatable<FilterbankCacheKey>
    {
        public FilterbankCacheKey(int bars, int sampleRate, int magLength)
        {
            Bars = bars;
            SampleRate = sampleRate;
            MagLength = magLength;
        }

        public int Bars { get; }
        public int SampleRate { get; }
        public int MagLength { get; }

        public bool Equals(FilterbankCacheKey other) =>
            Bars == other.Bars && SampleRate == other.SampleRate && MagLength == other.MagLength;
        public override bool Equals(object? obj) => obj is FilterbankCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Bars, SampleRate, MagLength);
    }

    private readonly struct FilterbankCache
    {
        public FilterbankCache(FilterbankCacheKey key, Filter[] filters)
        {
            Key = key;
            Filters = filters;
        }

        public FilterbankCacheKey Key { get; }
        public Filter[] Filters { get; }
        public bool HasValue => Filters != null;
    }

    private readonly struct Filter
    {
        public Filter(int start, int end, double[] weights)
        {
            Start = start;
            End = end;
            Weights = weights;
        }

        public int Start { get; }
        public int End { get; }
        public double[] Weights { get; }
    }
}
