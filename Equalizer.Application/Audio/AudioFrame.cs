namespace Equalizer.Application.Audio;

public sealed class AudioFrame
{
    public float[] Samples { get; }
    public int SampleRate { get; }

    public AudioFrame(float[] samples, int sampleRate)
    {
        Samples = samples;
        SampleRate = sampleRate;
    }
}
