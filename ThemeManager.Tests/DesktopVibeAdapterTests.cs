using ThemeManager.Core.NLP;
using ThemeManager.Core.Services;
using Xunit;

namespace ThemeManager.Tests;

public sealed class DesktopVibeAdapterTests
{
    [Fact]
    public void From_ClampsAndDerivesAtmosphere()
    {
        var signal = new VibeSignal
        {
            Saturation = 1f,
            SentimentValence = 1f,
            Warmth = 0.7f,
            MatchedKeywords = new() { "neon" }
        };

        var atmosphere = DesktopVibeAdapter.From(signal);

        Assert.Equal("Euphoric", atmosphere.Mood);
        Assert.InRange(atmosphere.Energy, 0, 1);
        Assert.InRange(atmosphere.Glow, 0, 1);
        Assert.InRange(atmosphere.Motion, 0, 1);
        Assert.InRange(atmosphere.AudioReactivity, 0, 1);
    }

    [Fact]
    public void Blend_IsSmoothAndBounded()
    {
        var a = DesktopVibeAdapter.From("Calm", 0.1, 0.1, 0.2);
        var b = DesktopVibeAdapter.From("Intense", 0.9, 0.9, 2.0);

        var midpoint = DesktopVibeAdapter.Blend(a, b, 0.5);

        Assert.Equal("Intense", midpoint.Mood);
        Assert.Equal(0.5, midpoint.Energy, 3);
        Assert.Equal(0.5, midpoint.Warmth, 3);
        Assert.Equal(1.1, midpoint.TransitionSeconds, 3);
    }
}
