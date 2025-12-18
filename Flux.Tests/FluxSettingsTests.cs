using System;
using Flux.Domain;

namespace Flux.Tests;

public class FluxSettingsTests
{
    [Fact]
    public void Default_IsWithinExpectedRanges()
    {
        var d = FluxSettings.Default;
        Assert.InRange(d.BarsCount, 8, 256);
        Assert.InRange(d.Responsiveness, 0, 1);
        Assert.InRange(d.Smoothing, 0, 1);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(400)]
    public void BarsCount_OutOfRange_Throws(int bars)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FluxSettings(bars, 0.5, 0.5, new ColorRgb(0, 0, 0)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Responsiveness_OutOfRange_Throws(double v)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FluxSettings(32, v, 0.5, new ColorRgb(0, 0, 0)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Smoothing_OutOfRange_Throws(double v)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FluxSettings(32, 0.5, v, new ColorRgb(0, 0, 0)));
    }
}
