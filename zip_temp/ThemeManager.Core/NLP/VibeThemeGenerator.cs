using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Public facade for the entire vibe-to-theme NLP pipeline.
/// Phase 3: exposes bigram matches, fuzzy corrections, and emoji flag in Explain().
/// </summary>
public sealed class VibeThemeGenerator
{
    public CozyTheme Generate(string vibeText)
    {
        var signal = VibeAnalyzer.Analyze(vibeText);

        GeneratedPalette palette = signal.HasSignal
            ? PaletteHarmonizer.Generate(signal)
            : new GeneratedPalette(
                "#F5F2EE","#E8E2D8","#C4AE98",
                "#8B6E58","#5C4035","#2E2018",
                "#7A6A60","#DDD5C8", false);

        var (name, description) = ThemeNameGenerator.Generate(signal);

        var theme = new CozyTheme
        {
            Id           = Guid.NewGuid().ToString(),
            Name         = name,
            Description  = description,
            IsBuiltIn    = false,
            LastModified = DateTimeOffset.UtcNow,
        };
        palette.ApplyTo(theme);
        return theme;
    }

    public VibeAnalysisResult Explain(string vibeText)
    {
        var signal  = VibeAnalyzer.Analyze(vibeText);
        var palette = signal.HasSignal ? PaletteHarmonizer.Generate(signal) : null;
        var (name, _) = ThemeNameGenerator.Generate(signal);

        return new VibeAnalysisResult(
            MatchedKeywords:    signal.MatchedKeywords,
            KeywordCategories:  signal.KeywordCategories,
            BigramMatches:      signal.BigramMatches,
            FuzzyCorrections:   signal.FuzzyCorrections,
            HadEmojiInput:      signal.HadEmojiInput,
            SentimentScore:     signal.SentimentValence,
            ComputedHue:        signal.Hue,
            ComputedLightness:  signal.Lightness,
            ComputedSaturation: signal.Saturation,
            WarmthBias:         signal.Warmth,
            IsDark:             signal.IsDark,
            GeneratedName:      name,
            Swatches:           palette?.Swatches().ToList() ?? new()
        );
    }

    public (CozyTheme Theme, VibeAnalysisResult Analysis) GenerateAndExplain(string vibeText)
    {
        var signal = VibeAnalyzer.Analyze(vibeText);

        GeneratedPalette palette = signal.HasSignal
            ? PaletteHarmonizer.Generate(signal)
            : new GeneratedPalette(
                "#F5F2EE","#E8E2D8","#C4AE98",
                "#8B6E58","#5C4035","#2E2018",
                "#7A6A60","#DDD5C8", false);

        var (name, description) = ThemeNameGenerator.Generate(signal);

        var theme = new CozyTheme
        {
            Id           = Guid.NewGuid().ToString(),
            Name         = name,
            Description  = description,
            IsBuiltIn    = false,
            LastModified = DateTimeOffset.UtcNow,
        };
        palette.ApplyTo(theme);

        var analysis = new VibeAnalysisResult(
            MatchedKeywords:    signal.MatchedKeywords,
            KeywordCategories:  signal.KeywordCategories,
            BigramMatches:      signal.BigramMatches,
            FuzzyCorrections:   signal.FuzzyCorrections,
            HadEmojiInput:      signal.HadEmojiInput,
            SentimentScore:     signal.SentimentValence,
            ComputedHue:        signal.Hue,
            ComputedLightness:  signal.Lightness,
            ComputedSaturation: signal.Saturation,
            WarmthBias:         signal.Warmth,
            IsDark:             signal.IsDark,
            GeneratedName:      name,
            Swatches:           palette.Swatches().ToList()
        );

        return (theme, analysis);
    }

    /// <summary>
    /// Conversational refinement — "make it darker", "more vibrant", "cooler" — patches the
    /// theme just generated instead of starting over. Mirrors WidgetVibeGenerator.Refine()'s
    /// pattern (mutate and return the same instance), but themes need one thing widgets don't:
    /// after nudging backgrounds/accents, TextPrimary/TextMuted might no longer have enough
    /// contrast against the new BackgroundBase, so this repairs that before returning rather
    /// than silently shipping a less-readable theme.
    /// </summary>
    public CozyTheme Refine(CozyTheme baseTheme, string instructionText)
    {
        var tokens = VibeTokenizer.TokenizeFull(instructionText).Raw;

        bool darker  = tokens.Any(w => w is "darker" or "dark" or "dim" or "dimmer");
        bool lighter = tokens.Any(w => w is "lighter" or "light" or "bright" or "brighter");
        bool vibrant = tokens.Any(w => w is "vibrant" or "vivid" or "saturated" or "bold" or "bolder" or "colorful");
        bool muted   = tokens.Any(w => w is "muted" or "desaturated" or "subtle" or "softer" or "pastel" or "faded");
        bool warmer  = tokens.Any(w => w is "warmer" or "warm" or "warmth" or "cozier");
        bool cooler  = tokens.Any(w => w is "cooler" or "cool" or "cold" or "colder" or "icy");

        double lightnessDelta   = darker ? -0.08 : lighter ? 0.08 : 0.0;
        double saturationDelta  = vibrant ? 0.10 : muted ? -0.10 : 0.0;
        double? warmthTargetHue = warmer ? 25.0 : cooler ? 215.0 : null;

        if (lightnessDelta == 0.0 && saturationDelta == 0.0 && warmthTargetHue is null)
            return baseTheme; // nothing recognized — leave it untouched rather than guessing

        // Backgrounds/border get a gentler saturation nudge than the accents — a "cozy" palette's
        // backgrounds are meant to stay fairly neutral even once the accent gets more vivid.
        baseTheme.BackgroundBase = AdjustColor(baseTheme.BackgroundBase, lightnessDelta, saturationDelta * 0.5, warmthTargetHue);
        baseTheme.BackgroundAlt  = AdjustColor(baseTheme.BackgroundAlt,  lightnessDelta, saturationDelta * 0.5, warmthTargetHue);
        baseTheme.Surface        = AdjustColor(baseTheme.Surface,       lightnessDelta, saturationDelta * 0.5, warmthTargetHue);
        baseTheme.BorderSubtle   = AdjustColor(baseTheme.BorderSubtle,  lightnessDelta, saturationDelta * 0.5, warmthTargetHue);
        baseTheme.AccentPrimary  = AdjustColor(baseTheme.AccentPrimary, lightnessDelta, saturationDelta, warmthTargetHue);
        baseTheme.AccentStrong   = AdjustColor(baseTheme.AccentStrong,  lightnessDelta, saturationDelta, warmthTargetHue);

        RepairTextContrast(baseTheme);
        return baseTheme;
    }

    /// <summary>Adjusts one hex color's lightness/saturation by a delta and, if a warmth target
    /// is given, nudges its hue partway there along the shortest rotational path (20% per call,
    /// not a snap — repeated "warmer" instructions converge on it gradually, same compounding
    /// behavior as the widget side's repeated "bigger").</summary>
    private static string AdjustColor(string hex, double lightnessDelta, double saturationDelta, double? warmthTargetHue)
    {
        var (h, s, l) = ColorMath.HexToHsl(hex);

        if (warmthTargetHue is { } target)
        {
            float dh = (float)target - h;
            if (dh > 180f) dh -= 360f;
            if (dh < -180f) dh += 360f;
            h += dh * 0.2f;
        }

        s = Math.Clamp(s + (float)saturationDelta, 0f, 1f);
        l = Math.Clamp(l + (float)lightnessDelta, 0.05f, 0.95f);

        return ColorMath.HslToHex(h, s, l);
    }

    /// <summary>After a refinement changes the backgrounds, text colors might no longer clear a
    /// safe contrast ratio against them — nudge lightness away from the background (not toward
    /// any specific target) until they do, bounded so this can never loop forever.</summary>
    private static void RepairTextContrast(CozyTheme theme)
    {
        theme.TextPrimary = EnsureContrast(theme.TextPrimary, theme.BackgroundBase, minRatio: 4.5f);
        theme.TextMuted    = EnsureContrast(theme.TextMuted,   theme.BackgroundBase, minRatio: 3.0f);
    }

    private static string EnsureContrast(string foregroundHex, string backgroundHex, float minRatio)
    {
        var (h, s, l) = ColorMath.HexToHsl(foregroundHex);
        bool foregroundShouldBeDark = ColorMath.LuminanceFromHex(backgroundHex) > 0.5f;

        for (int i = 0; i < 12 && ColorMath.ContrastRatio(ColorMath.HslToHex(h, s, l), backgroundHex) < minRatio; i++)
            l = Math.Clamp(l + (foregroundShouldBeDark ? -0.05f : 0.05f), 0.02f, 0.98f);

        return ColorMath.HslToHex(h, s, l);
    }
}

public sealed record VibeAnalysisResult(
    List<string>               MatchedKeywords,
    Dictionary<string, string> KeywordCategories,
    List<string>               BigramMatches,          // Phase 3
    Dictionary<string, string> FuzzyCorrections,       // Phase 3
    bool                       HadEmojiInput,          // Phase 3
    float SentimentScore,
    float ComputedHue,
    float ComputedLightness,
    float ComputedSaturation,
    float WarmthBias,
    bool  IsDark,
    string GeneratedName,
    List<string> Swatches
);
