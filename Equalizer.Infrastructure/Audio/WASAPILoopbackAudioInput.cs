using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Equalizer.Application.Abstractions;
using Equalizer.Application.Audio;
using NAudio.Wave;

namespace Equalizer.Infrastructure.Audio;

public sealed class WASAPILoopbackAudioInput : IAudioInputPort, IDisposable
{
    private readonly WasapiLoopbackCapture _capture;
    private readonly object _lock = new();
    private readonly Queue<float> _queue = new();
    private readonly SemaphoreSlim _dataAvailable = new(0);
    private bool _disposed;

    public int SampleRate { get; }
    public int Channels { get; }

    public WASAPILoopbackAudioInput()
    {
        _capture = new WasapiLoopbackCapture();
        SampleRate = _capture.WaveFormat.SampleRate;
        Channels = _capture.WaveFormat.Channels;
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += (_, __) => _dataAvailable.Release();
        _capture.StartRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || e.BytesRecorded <= 0) return;
        var wf = _capture.WaveFormat;
        int channels = wf.Channels;
        try
        {
            if (wf.Encoding == WaveFormatEncoding.IeeeFloat && wf.BitsPerSample == 32)
            {
                var wb = new WaveBuffer(e.Buffer);
                int sampleCount = e.BytesRecorded / sizeof(float);
                if (channels == 1)
                {
                    lock (_lock)
                    {
                        for (int i = 0; i < sampleCount; i++)
                            _queue.Enqueue(wb.FloatBuffer[i]);
                    }
                }
                else
                {
                    int frames = sampleCount / channels;
                    lock (_lock)
                    {
                        int idx = 0;
                        for (int f = 0; f < frames; f++)
                        {
                            double sum = 0;
                            for (int c = 0; c < channels; c++)
                                sum += wb.FloatBuffer[idx++];
                            _queue.Enqueue((float)(sum / channels));
                        }
                    }
                }
            }
            else if (wf.Encoding == WaveFormatEncoding.Pcm && wf.BitsPerSample == 16)
            {
                int sampleCount = e.BytesRecorded / sizeof(short);
                var shorts = new short[sampleCount];
                Buffer.BlockCopy(e.Buffer, 0, shorts, 0, e.BytesRecorded);
                if (channels == 1)
                {
                    lock (_lock)
                    {
                        for (int i = 0; i < sampleCount; i++)
                            _queue.Enqueue(shorts[i] / 32768f);
                    }
                }
                else
                {
                    int frames = sampleCount / channels;
                    lock (_lock)
                    {
                        int idx = 0;
                        for (int f = 0; f < frames; f++)
                        {
                            int sum = 0;
                            for (int c = 0; c < channels; c++)
                                sum += shorts[idx++];
                            _queue.Enqueue(sum / (32768f * channels));
                        }
                    }
                }
            }
            else
            {
                // Unsupported format: ignore
                return;
            }
        }
        finally
        {
            _dataAvailable.Release();
        }
    }

    public async Task<AudioFrame> ReadFrameAsync(int minSamples, CancellationToken cancellationToken)
    {
        if (minSamples <= 0) minSamples = 1024;
        float[] buffer = new float[minSamples];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int copied = 0;
            lock (_lock)
            {
                while (_queue.Count > 0 && copied < minSamples)
                {
                    buffer[copied++] = _queue.Dequeue();
                }
            }
            if (copied >= minSamples)
            {
                if (copied == minSamples)
                    return new AudioFrame(buffer, SampleRate);
                // If more were available (shouldn't happen with the logic above), truncate.
                var exact = new float[minSamples];
                Array.Copy(buffer, exact, minSamples);
                return new AudioFrame(exact, SampleRate);
            }
            await _dataAvailable.WaitAsync(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _capture.StopRecording();
        }
        catch { }
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _dataAvailable.Dispose();
    }
}
