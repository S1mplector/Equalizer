namespace Equalizer.Application.Models;

public sealed class VisualizerFrame
{
    public float[] Bars { get; }
    public float Bass { get; }
    public float Mid { get; }
    public float Treble { get; }
    public bool IsBeat { get; }
    public float BeatStrength { get; }

    public VisualizerFrame(float[] bars, float bass, float mid, float treble, bool isBeat, float beatStrength)
    {
        Bars = bars;
        Bass = bass;
        Mid = mid;
        Treble = treble;
        IsBeat = isBeat;
        BeatStrength = beatStrength;
    }
}
