using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Converts a <see cref="VibeSignal"/> into the eight hex tokens of a <see cref="CozyTheme"/>
/// using perceptually uniform OKLCH tonal roles.
///
/// Design rules: coherent dark/light surfaces; a restrained primary hue; a readable primary
/// accent; a contrasting secondary accent when a second distinct hue was explicitly requested;
/// neutral high-contrast text; and a visible but quiet border. Gamut mapping and the shared
/// PaletteQualityValidator make the result safe for real UI use.
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
    /// Harmony-lock entry point: derives an art-directed palette from the chosen accent while
    /// preserving the caller's existing light/dark direction. Because accent colors are often
    /// intentionally bright on dark palettes or dark on light palettes, polarity should be supplied
    /// by the edited theme rather than guessed from the accent alone.
    /// </summary>
    public static GeneratedPalette FromAccentHex(string accentHex, bool? preferDark = null)
    {
        var (accentL, accentC, accentHue) = ColorMath.HexToOklch(accentHex);
        bool isDark = preferDark ?? accentL >= 0.60f;

        var synth = new VibeSignal
        {
            Hue = accentHue,
            PrimaryPaletteHue = accentHue,
            HasPrimaryPaletteHue = true,
            Lightness = isDark ? 0.38f : 0.68f,
            Saturation = Math.Clamp(accentC / 0.22f, 0.12f, 0.95f),
            Warmth = 0f,
            SentimentValence = 0f,
        };
        synth.MatchedKeywords.Add("custom");
        synth.KeywordCategories["custom"] = "color";

        var palette = Generate(synth);
        // Harmony lock means the chosen accent is the anchor; return it exactly and let the shared
        // quality validator adjust only if the user's chosen contrast makes that necessary.
        return palette with
        {
            AccentPrimary = CozyTheme.NormalizeHex(accentHex),
            IsDark = isDark
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
