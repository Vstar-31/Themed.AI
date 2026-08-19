using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="ThemeInterpolator"/> — the crossfade color math behind Phase 7's
/// automatic theme switching.
/// </summary>
public class ThemeInterpolatorTests
{
    private static CozyTheme MakeTheme(string bg, string accent = "#000000") => new()
    {
        Id = "test",
        Name = "Test",
        BackgroundBase = bg,
        AccentPrimary = accent,
    };

    // ── Endpoints ────────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_AtZero_EqualsFromColors()
    {
        var from = MakeTheme("#000000");
        var to = MakeTheme("#FFFFFF");

        var result = ThemeInterpolator.Lerp(from, to, 0.0);

        Assert.Equal("#000000", result.BackgroundBase);
    }

    [Fact]
    public void Lerp_AtOne_EqualsToColors()
    {
        var from = MakeTheme("#000000");
        var to = MakeTheme("#FFFFFF");

        var result = ThemeInterpolator.Lerp(from, to, 1.0);

        Assert.Equal("#FFFFFF", result.BackgroundBase);
    }

    [Fact]
    public void Lerp_AtHalf_IsMidpoint()
    {
        var from = MakeTheme("#000000");
        var to = MakeTheme("#FFFFFF");

        var result = ThemeInterpolator.Lerp(from, to, 0.5);

        // 0x00 -> 0x80 (round(127.5) = 128 = 0x80) for each channel
        Assert.Equal("#808080", result.BackgroundBase);
    }

    // ── Clamping ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1.0)]
    [InlineData(2.5)]
    public void Lerp_ClampsOutOfRangeT(double t)
    {
        var from = MakeTheme("#000000");
        var to = MakeTheme("#FFFFFF");

        var result = ThemeInterpolator.Lerp(from, to, t);

        Assert.True(result.BackgroundBase is "#000000" or "#FFFFFF");
    }

    // ── Identity fields ──────────────────────────────────────────────────────

    [Fact]
    public void Lerp_TakesIdentityFromTargetTheme()
    {
        var from = new CozyTheme { Id = "from-id", Name = "From", IsBuiltIn = true };
        var to = new CozyTheme { Id = "to-id", Name = "To", IsBuiltIn = false };

        var result = ThemeInterpolator.Lerp(from, to, 0.5);

        Assert.Equal("to-id", result.Id);
        Assert.Equal("To", result.Name);
        Assert.False(result.IsBuiltIn);
    }

    // ── Malformed input ──────────────────────────────────────────────────────

    [Fact]
    public void Lerp_MalformedHex_DoesNotThrow()
    {
        var from = MakeTheme("not-a-color");
        var to = MakeTheme("#FFFFFF");

        var exception = Record.Exception(() => ThemeInterpolator.Lerp(from, to, 0.5));

        Assert.Null(exception);
    }

    // ── Geometry ─────────────────────────────────────────────────────────────

    [Fact]
    public void Lerp_InterpolatesCornerRadiusAndDensity()
    {
        var from = new CozyTheme { CornerRadiusScale = 0.0, DensityScale = 0.0 };
        var to = new CozyTheme { CornerRadiusScale = 2.0, DensityScale = 1.0 };

        var result = ThemeInterpolator.Lerp(from, to, 0.5);

        Assert.Equal(1.0, result.CornerRadiusScale);
        Assert.Equal(0.5, result.DensityScale);
    }
}
