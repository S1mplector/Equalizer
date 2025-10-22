using System;
using System.Threading;
using System.Threading.Tasks;
using Equalizer.Application.Abstractions;

namespace Equalizer.Infrastructure.Audio;

public sealed class RandomAudioInput : IAudioInputPort
{
    private readonly Random _rng = new();
    private float _phase;

    public Task<float[]> GetSpectrumAsync(int bars, CancellationToken cancellationToken)
    {
        var data = new float[bars];
        _phase += 0.15f;
        for (int i = 0; i < bars; i++)
        {
            var t = (float)i / Math.Max(1, bars - 1);
            var baseWave = (float)(0.6 + 0.4 * Math.Sin(2 * Math.PI * (t * 2 + _phase)));
            var noise = (float)(_rng.NextDouble() * 0.3);
            data[i] = Math.Clamp(baseWave + noise, 0f, 1f);
        }
        return Task.FromResult(data);
    }
}
