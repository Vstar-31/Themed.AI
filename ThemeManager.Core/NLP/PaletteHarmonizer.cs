using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Converts a <see cref="VibeSignal"/> into the 8 hex color tokens of a <see cref="CozyTheme"/>
/// using perceptually uniform OKLCH space for the tonal ramp.
///
/// Design rules baked in:
///   • BackgroundBase — primary hue, high L, low S (airy, readable base)
///   • BackgroundAlt  — +12° hue shift (analogous), slightly darker/more saturated
///   • Surface        — +25° shift, mid L/S (tactile controls and buttons)
///   • AccentPrimary  — base hue, lower L, richer S (interactive elements)
///   • AccentStrong   — base hue, lowest L, full S (headers, borders, emphasis)
///   • TextPrimary    — near-complementary hue, very dark (readable on all backgrounds)
///   • TextMuted      — same as TextPrimary but higher L, much lower S
///   • BorderSubtle   — BackgroundAlt hue, mid-high L, very low S (hairlines)
///
/// Python analogy: colorsys + numpy.clip — same math, no dependencies.
/// </summary>
public static class PaletteHarmonizer
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a complete palette from a <see cref="VibeSignal"/> using OKLCH roles and hue
    /// relationships. Final WCAG contrast repair is performed by PaletteQualityValidator upstream.
    /// All returned hex strings are in #RRGGBB format.
    /// </summary>
    public static GeneratedPalette Generate(VibeSignal signal)
    {
        float h = signal.HasPrimaryPaletteHue ? signal.PrimaryPaletteHue : signal.Hue;
        float l = signal.Lightness;
        float s = signal.Saturation;

        // Explicit multi-color prompts retain their strongest hue as the primary identity instead
        // of circular-averaging two opposing colors into a third, unintended color family.
        // Warmth still applies as a subtle art-direction nudge (capped at ±18°).
        float warmNudge = signal.Warmth * 18f;
        h = WrapHue(h + warmNudge);

        // ── Perceptual light/dark composition ─────────────────────────────────
        // Hue comes from the weighted NLP signal; OKLCH lightness/chroma control how strongly
        // each role appears. These tonal relationships are more consistent across colors than
        // the old HSL-only lightness offsets (especially for yellow vs. blue).
        bool isDark = signal.IsDark;
        float inputLightness = Math.Clamp(l, 0f, 1f);
        float inputSaturation = Math.Clamp(s, 0f, 1f);

        float backgroundL = isDark
            ? Math.Clamp(0.115f + inputLightness * 0.085f, 0.105f, 0.16f)
            : Math.Clamp(0.925f + (inputLightness - 0.55f) * 0.08f, 0.90f, 0.96f);
        float backgroundChroma = 0.015f + inputSaturation * 0.045f;
        float accentChroma = 0.105f + inputSaturation * 0.105f;
        float strongChroma = 0.075f + inputSaturation * 0.085f;
        float textHue = WrapHue(h + (signal.Warmth < -0.15f ? 188f : 165f));
        float highlightHue = signal.HasSecondaryPaletteHue
            ? WrapHue(signal.SecondaryPaletteHue + signal.Warmth * 8f)
            : WrapHue(h + (signal.Warmth >= 0f ? 34f : -34f));

        string bgBase = ColorMath.OklchToHex(backgroundL, backgroundChroma, h);
        string bgAlt = ColorMath.OklchToHex(
            isDark ? backgroundL + 0.035f : backgroundL - 0.035f,
            backgroundChroma + 0.008f, WrapHue(h + 8f));
        string surface = ColorMath.OklchToHex(
            isDark ? backgroundL + 0.070f : backgroundL - 0.075f,
            backgroundChroma + 0.020f, WrapHue(h + 18f));

        string accentPrimary = ColorMath.OklchToHex(
            isDark ? 0.73f : 0.48f,
            accentChroma, h);
        string accentStrong = ColorMath.OklchToHex(
            isDark ? 0.82f : 0.36f,
            strongChroma, highlightHue);

        string textPrimary = ColorMath.OklchToHex(
            isDark ? 0.965f : 0.22f, 0.012f, textHue);
        string textMuted = ColorMath.OklchToHex(
            isDark ? 0.82f : 0.40f, 0.020f, textHue);
        string borderSubtle = ColorMath.OklchToHex(
            isDark ? 0.33f : 0.74f, 0.025f, WrapHue(h + 8f));

        return new GeneratedPalette(
            bgBase, bgAlt, surface,
            accentPrimary, accentStrong,
            textPrimary, textMuted, borderSubtle,
            isDark);
    }

    /// <summary>
    /// Harmony-lock entry point: given a chosen AccentPrimary hex, back-calculate
    /// an approximate base HSL and re-derive all 8 palette slots.
    ///
    /// Used by <see cref="ThemeEditorViewModel"/> when HarmonyLocked = true and
    /// the user drags AccentPrimary — the rest of the palette follows automatically.
    /// </summary>
    public static GeneratedPalette FromAccentHex(string accentHex)
    {
        var (h, s, l) = ColorMath.HexToHsl(accentHex);

        // Infer whether the user wants a dark or light palette from the accent lightness.
        bool isDark = l < 0.40f;

        // Back-project the accent HSL into an approximate "base" lightness
        // by reversing the offset applied in Generate():
        //   accentPrimary = ToHex(h, s * 0.75, l - 0.32)  →  base l ≈ accent_l + 0.32
        float baseL = isDark
            ? Math.Clamp(l - 0.30f, 0.05f, 0.38f)
            : Math.Clamp(l + 0.32f, 0.55f, 0.92f);

        float baseS = isDark
            ? Math.Clamp(s / 0.80f, 0.08f, 0.60f)
            : Math.Clamp(s / 0.75f, 0.05f, 0.55f);

        // Synthesise a minimal VibeSignal so we can reuse the same Generate() path.
        var synth = new VibeSignal
        {
            Hue        = h,
            Lightness  = baseL,
            Saturation = baseS,
            Warmth     = 0f,
            SentimentValence = 0f,
        };
        // Force a dummy keyword so HasSignal = true.
        synth.MatchedKeywords.Add("custom");
        synth.KeywordCategories["custom"] = "color";

        return Generate(synth);
    }

    // ── HSL → RGB → Hex conversion ────────────────────────────────────────────
    // Pure C# port of the standard CSS Color Level 4 algorithm.
    // Python equivalent: colorsys.hls_to_rgb (note: Python uses HLS order not HSL).

    /// <summary>Converts HSL values to a #RRGGBB hex string.</summary>
    private static string ToHex(float hueDeg, float saturation, float lightness)
    {
        saturation = Math.Clamp(saturation, 0f, 1f);
        lightness  = Math.Clamp(lightness,  0f, 1f);
        hueDeg     = WrapHue(hueDeg);

        float h = hueDeg / 360f;
        float s = saturation;
        float l = lightness;

        float r, g, b;
        if (s == 0f)
        {
            r = g = b = l; // achromatic
        }
        else
        {
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            r = HueToRgb(p, q, h + 1f / 3f);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1f / 3f);
        }

        int ri = (int)Math.Round(r * 255);
        int gi = (int)Math.Round(g * 255);
        int bi = (int)Math.Round(b * 255);
        return $"#{Math.Clamp(ri,0,255):X2}{Math.Clamp(gi,0,255):X2}{Math.Clamp(bi,0,255):X2}";
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1f;
        if (t > 1) t -= 1f;
        return t switch
        {
            < 1f/6f => p + (q - p) * 6f * t,
            < 1f/2f => q,
            < 2f/3f => p + (q - p) * (2f/3f - t) * 6f,
            _        => p,
        };
    }

    private static float WrapHue(float h) => ((h % 360f) + 360f) % 360f;
    private static float Clamp(float v)   => Math.Clamp(v, 0.04f, 0.96f);
}

/// <summary>The 8-slot palette produced by <see cref="PaletteHarmonizer"/>.</summary>
public sealed record GeneratedPalette(
    string BackgroundBase,
    string BackgroundAlt,
    string Surface,
    string AccentPrimary,
    string AccentStrong,
    string TextPrimary,
    string TextMuted,
    string BorderSubtle,
    bool   IsDark
)
{
    /// <summary>Applies this palette to an existing (or new) <see cref="CozyTheme"/>.</summary>
    public void ApplyTo(CozyTheme theme)
    {
        theme.BackgroundBase  = BackgroundBase;
        theme.BackgroundAlt   = BackgroundAlt;
        theme.Surface         = Surface;
        theme.AccentPrimary   = AccentPrimary;
        theme.AccentStrong    = AccentStrong;
        theme.TextPrimary     = TextPrimary;
        theme.TextMuted       = TextMuted;
        theme.BorderSubtle    = BorderSubtle;
        theme.LastModified    = DateTimeOffset.UtcNow;
    }

    /// <summary>All 5 display swatches in order: bg, alt, surface, accent, strong.</summary>
    public IEnumerable<string> Swatches()
        => [BackgroundBase, BackgroundAlt, Surface, AccentPrimary, AccentStrong];
}
