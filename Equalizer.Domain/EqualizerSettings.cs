namespace Equalizer.Domain;

public sealed class EqualizerSettings
{
    public int BarsCount { get; }
    public double Responsiveness { get; }
    public double Smoothing { get; }
    public ColorRgb Color { get; }

    public EqualizerSettings(int barsCount, double responsiveness, double smoothing, ColorRgb color)
    {
        if (barsCount < 8 || barsCount > 256)
            throw new ArgumentOutOfRangeException(nameof(barsCount), "BarsCount must be between 8 and 256.");
        if (responsiveness < 0 || responsiveness > 1)
            throw new ArgumentOutOfRangeException(nameof(responsiveness), "Responsiveness must be between 0 and 1.");
        if (smoothing < 0 || smoothing > 1)
            throw new ArgumentOutOfRangeException(nameof(smoothing), "Smoothing must be between 0 and 1.");

        BarsCount = barsCount;
        Responsiveness = responsiveness;
        Smoothing = smoothing;
        Color = color;
    }

    public static EqualizerSettings Default => new(
        barsCount: 64,
        responsiveness: 0.7,
        smoothing: 0.5,
        color: new ColorRgb(0, 255, 128)
    );
}
