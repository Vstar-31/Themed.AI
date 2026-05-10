using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Converts a <see cref="VibeSignal"/> into the 8 hex color tokens of a <see cref="CozyTheme"/>
/// using perceptual color theory in HSL space.
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
    /// Builds a complete palette from a <see cref="VibeSignal"/>.
    /// All returned hex strings are in #RRGGBB format.
    /// </summary>
    public static GeneratedPalette Generate(VibeSignal signal)
    {
        float h = signal.Hue;
        float l = signal.Lightness;
        float s = signal.Saturation;

        // Apply warmth bias — nudge hue toward warm (orange) or cool (cyan) pole.
        // Max nudge ±18° so the palette stays in the right color family.
        float warmNudge = signal.Warmth * 18f;
        h = WrapHue(h + warmNudge);

        // ── Dark mode vs light mode ────────────────────────────────────────────
        bool isDark = signal.IsDark;

        // Clamp base lightness to readable ranges.
        if (isDark)
        {
            l = Math.Clamp(l, 0.05f, 0.38f);
            s = Math.Clamp(s, 0.08f, 0.60f);
        }
        else
        {
            l = Math.Clamp(l, 0.55f, 0.92f);
            s = Math.Clamp(s, 0.05f, 0.55f);
        }

        // ── Generate the 8 palette slots ──────────────────────────────────────
        string bgBase, bgAlt, surface, accentPrimary, accentStrong,
               textPrimary, textMuted, borderSubtle;

        if (isDark)
        {
            // Dark palette — backgrounds are dark, accents glow
            bgBase        = ToHex(h,           s * 0.30f, l);
            bgAlt         = ToHex(WrapHue(h + 8f),  s * 0.38f, Clamp(l + 0.06f));
            surface       = ToHex(WrapHue(h + 18f), s * 0.55f, Clamp(l + 0.14f));
            accentPrimary = ToHex(h,           s * 0.80f, Clamp(l + 0.30f));
            accentStrong  = ToHex(h,           s * 0.95f, Clamp(l + 0.48f));

            // Text on dark: near-complementary hue, very high lightness
            float textH   = WrapHue(h + 175f);
            textPrimary   = ToHex(textH, s * 0.12f, 0.88f);
            textMuted     = ToHex(textH, s * 0.08f, 0.58f);
            borderSubtle  = ToHex(WrapHue(h + 8f), s * 0.20f, Clamp(l + 0.18f));
        }
        else
        {
            // Light palette — soft backgrounds, rich accents
            bgBase        = ToHex(h,           s * 0.18f, l);
            bgAlt         = ToHex(WrapHue(h + 10f), s * 0.26f, Clamp(l - 0.08f));
            surface       = ToHex(WrapHue(h + 22f), s * 0.50f, Clamp(l - 0.20f));
            accentPrimary = ToHex(h,           s * 0.75f, Clamp(l - 0.32f));
            accentStrong  = ToHex(h,           s * 0.90f, Clamp(l - 0.45f));

            // Text on light: near-complementary, very dark
            float textH   = WrapHue(h + 175f);
            textPrimary   = ToHex(textH, s * 0.25f, 0.14f);
            textMuted     = ToHex(textH, s * 0.12f, 0.46f);
            borderSubtle  = ToHex(WrapHue(h + 10f), s * 0.15f, Clamp(l - 0.10f));
        }

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
