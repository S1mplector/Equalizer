namespace Equalizer.Domain;

public readonly struct ColorRgb
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public ColorRgb(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }
}
