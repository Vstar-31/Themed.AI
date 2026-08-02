using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="ColorMath"/> — hex parsing, RGB↔HSL conversion,
/// WCAG luminance, contrast ratio, and HSL interpolation.
/// </summary>
public class ColorMathTests
{
    // ── Hex Parsing ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("#FF0000", 1f, 0f, 0f)]
    [InlineData("#00FF00", 0f, 1f, 0f)]
    [InlineData("#0000FF", 0f, 0f, 1f)]
    [InlineData("#000000", 0f, 0f, 0f)]
    [InlineData("#FFFFFF", 1f, 1f, 1f)]
    public void HexToRgb_ParsesStandard6Digit(string hex, float er, float eg, float eb)
    {
        var (r, g, b) = ColorMath.HexToRgb(hex);
        Assert.Equal(er, r, 0.01f);
        Assert.Equal(eg, g, 0.01f);
        Assert.Equal(eb, b, 0.01f);
    }

    [Fact]
    public void HexToRgb_ExpandsShorthand3Digit()
    {
        var (r, g, b) = ColorMath.HexToRgb("#F0A");
        // #F0A → #FF00AA
        Assert.Equal(1f, r, 0.01f);
        Assert.Equal(0f, g, 0.01f);
        Assert.True(b > 0.6f); // 0xAA / 255 ≈ 0.667
    }

    [Fact]
    public void HexToRgb_InvalidHex_ReturnsBlack()
    {
        var (r, g, b) = ColorMath.HexToRgb("#XY");
        Assert.Equal(0f, r);
        Assert.Equal(0f, g);
        Assert.Equal(0f, b);
    }

    [Fact]
    public void HexToRgb_WithoutHashPrefix_StillWorks()
    {
        var (r, g, b) = ColorMath.HexToRgb("FF0000");
        Assert.Equal(1f, r, 0.01f);
    }

    // ── RGB → Hex ────────────────────────────────────────────────────────────

    [Fact]
    public void RgbToHex_ProducesUppercase6Digit()
    {
        string hex = ColorMath.RgbToHex(1f, 0f, 0f);
        Assert.Equal("#FF0000", hex);
    }

    [Fact]
    public void RgbToHex_ClampsOutOfRange()
    {
        string hex = ColorMath.RgbToHex(2f, -1f, 0.5f);
        Assert.Equal("#FF0080", hex);
    }

    // ── RGB ↔ HSL roundtrip ──────────────────────────────────────────────────

    [Theory]
    [InlineData("#FF0000", 0f)]    // Pure red → hue = 0°
    [InlineData("#00FF00", 120f)]  // Pure green → hue = 120°
    [InlineData("#0000FF", 240f)]  // Pure blue → hue = 240°
    public void RgbToHsl_PrimaryColors(string hex, float expectedHue)
    {
        var (h, s, l) = ColorMath.HexToHsl(hex);
        Assert.Equal(expectedHue, h, 1f);
        Assert.Equal(1f, s, 0.01f);
        Assert.Equal(0.5f, l, 0.01f);
    }

    [Fact]
    public void RgbToHsl_White_IsAchromatic()
    {
        var (h, s, l) = ColorMath.HexToHsl("#FFFFFF");
        Assert.Equal(0f, s, 0.01f);
        Assert.Equal(1f, l, 0.01f);
    }

    [Fact]
    public void RgbToHsl_Black_IsAchromatic()
    {
        var (h, s, l) = ColorMath.HexToHsl("#000000");
        Assert.Equal(0f, s, 0.01f);
        Assert.Equal(0f, l, 0.01f);
    }

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#7D5A44")]  // Cocoa (the app's accent)
    [InlineData("#F5F1EA")]  // Linen (the app's background)
    [InlineData("#4A342A")]  // Espresso
    public void HslRoundtrip_IsLossless(string hex)
    {
        var (r, g, b) = ColorMath.HexToRgb(hex);
        var (h, s, l) = ColorMath.RgbToHsl(r, g, b);
        var (r2, g2, b2) = ColorMath.HslToRgb(h, s, l);

        Assert.Equal(r, r2, 0.01f);
        Assert.Equal(g, g2, 0.01f);
        Assert.Equal(b, b2, 0.01f);
    }

    // ── WCAG Luminance ───────────────────────────────────────────────────────

    [Fact]
    public void RelativeLuminance_Black_IsZero()
    {
        float lum = ColorMath.RelativeLuminance(0f, 0f, 0f);
        Assert.Equal(0f, lum, 0.001f);
    }

    [Fact]
    public void RelativeLuminance_White_IsOne()
    {
        float lum = ColorMath.RelativeLuminance(1f, 1f, 1f);
        Assert.Equal(1f, lum, 0.001f);
    }

    [Fact]
    public void LuminanceFromHex_Convenience()
    {
        float lum = ColorMath.LuminanceFromHex("#FFFFFF");
        Assert.Equal(1f, lum, 0.001f);
    }

    // ── Contrast Ratio ───────────────────────────────────────────────────────

    [Fact]
    public void ContrastRatio_BlackOnWhite_Is21()
    {
        float ratio = ColorMath.ContrastRatio("#000000", "#FFFFFF");
        Assert.Equal(21f, ratio, 0.5f);
    }

    [Fact]
    public void ContrastRatio_SameColor_Is1()
    {
        float ratio = ColorMath.ContrastRatio("#7D5A44", "#7D5A44");
        Assert.Equal(1f, ratio, 0.01f);
    }

    [Fact]
    public void ContrastRatio_IsSymmetric()
    {
        float a = ColorMath.ContrastRatio("#7D5A44", "#F5F1EA");
        float b = ColorMath.ContrastRatio("#F5F1EA", "#7D5A44");
        Assert.Equal(a, b, 0.001f);
    }

    // ── LerpHex ──────────────────────────────────────────────────────────────

    [Fact]
    public void LerpHex_AtZero_ReturnsColorA()
    {
        string result = ColorMath.LerpHex("#FF0000", "#0000FF", 0f);
        Assert.Equal("#FF0000", result);
    }

    [Fact]
    public void LerpHex_AtOne_ReturnsColorB()
    {
        string result = ColorMath.LerpHex("#FF0000", "#0000FF", 1f);
        Assert.Equal("#0000FF", result);
    }

    [Fact]
    public void LerpHex_AtHalf_ProducesMidpoint()
    {
        string result = ColorMath.LerpHex("#000000", "#FFFFFF", 0.5f);
        // Midpoint of black and white in HSL = 50% lightness = middle grey
        var (r, g, b2) = ColorMath.HexToRgb(result);
        Assert.InRange(r, 0.4f, 0.6f);
    }

    [Fact]
    public void LerpHex_ClampsOvershoot()
    {
        string result = ColorMath.LerpHex("#FF0000", "#0000FF", 5f);
        Assert.Equal("#0000FF", result);
    }
}
