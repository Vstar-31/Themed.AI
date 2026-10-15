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
        => GenerateAndExplain(vibeText).Theme;

    public VibeAnalysisResult Explain(string vibeText)
        => GenerateAndExplain(vibeText).Analysis;

    /// <summary>
    /// Generates and explains a palette in one pass. Exact aesthetic/pop-culture references use a
    /// curated art-directed profile; free-form prompts use the compositional NLP engine. Both paths
    /// go through the same contrast/tonal quality gate before the theme is returned.
    /// </summary>
    public (CozyTheme Theme, VibeAnalysisResult Analysis) GenerateAndExplain(string vibeText)
    {
        var signal = VibeAnalyzer.Analyze(vibeText);
        var profile = AestheticProfileCatalog.Find(vibeText);

        CozyTheme theme;
        if (profile is not null)
        {
            theme = AestheticProfileCatalog.CreateTheme(profile, vibeText);
        }
        else
        {
            GeneratedPalette palette = signal.HasSignal
                ? PaletteHarmonizer.Generate(signal)
                : new GeneratedPalette(
                    "#F5F2EE", "#E8E2D8", "#C4AE98",
                    "#8B6E58", "#5C4035", "#2E2018",
                    "#7A6A60", "#DDD5C8", false);

            var (name, description) = ThemeNameGenerator.Generate(signal);
            theme = new CozyTheme
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                IsBuiltIn = false,
                LastModified = DateTimeOffset.UtcNow,
            };
            palette.ApplyTo(theme);
            PaletteQualityValidator.ValidateAndRepair(theme, signal.HasSignal ? signal.IsDark : null);
        }

        var analysis = BuildAnalysis(signal, theme, profile);
        return (theme, analysis);
    }

    private static VibeAnalysisResult BuildAnalysis(
        VibeSignal signal,
        CozyTheme theme,
        AestheticProfileCatalog.Profile? profile)
    {
        var keywords = signal.MatchedKeywords.ToList();
        var categories = new Dictionary<string, string>(signal.KeywordCategories, StringComparer.OrdinalIgnoreCase);
        var bigrams = signal.BigramMatches.ToList();
        var fuzzy = new Dictionary<string, string>(signal.FuzzyCorrections, StringComparer.OrdinalIgnoreCase);

        if (profile is not null)
        {
            // Surface the detected visual language in the insight panel even when the alias itself
            // is not a color-lexicon word (e.g. "Neo Deco", "Frutiger Aero", or "Gimme Gummy").
            if (!keywords.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
                keywords.Insert(0, profile.Name);
            categories[profile.Name] = "aesthetic profile";
        }

        var (hue, saturation, _) = ColorMath.HexToHsl(theme.AccentPrimary);
        var (_, _, lightness) = ColorMath.HexToHsl(theme.BackgroundBase);
        var isDark = ColorMath.LuminanceFromHex(theme.BackgroundBase) < 0.18f;

        return new VibeAnalysisResult(
            MatchedKeywords: keywords,
            KeywordCategories: categories,
            BigramMatches: bigrams,
            FuzzyCorrections: fuzzy,
            HadEmojiInput: signal.HadEmojiInput,
            SentimentScore: signal.SentimentValence,
            ComputedHue: hue,
            ComputedLightness: lightness,
            ComputedSaturation: saturation,
            WarmthBias: signal.Warmth,
            IsDark: isDark,
            GeneratedName: theme.Name,
            Swatches: [theme.BackgroundBase, theme.BackgroundAlt, theme.Surface, theme.AccentPrimary, theme.AccentStrong]);
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

        PaletteQualityValidator.ValidateAndRepair(baseTheme);
        return baseTheme;
    }

    /// <summary>
    /// Applies a small perceptual OKLCH adjustment. Chroma is changed independently from lightness,
    /// so "more vibrant" won't unexpectedly wash the color out as HSL ramps often do.
    /// </summary>
    private static string AdjustColor(string hex, double lightnessDelta, double saturationDelta, double? warmthTargetHue)
    {
        var (lightness, chroma, hue) = ColorMath.HexToOklch(hex);
        if (warmthTargetHue is { } target)
        {
            float dh = (float)target - hue;
            if (dh > 180f) dh -= 360f;
            if (dh < -180f) dh += 360f;
            hue += dh * 0.2f;
        }

        float nextLightness = Math.Clamp(lightness + (float)lightnessDelta * 0.65f, 0.04f, 0.98f);
        float nextChroma = Math.Clamp(chroma + (float)saturationDelta * 0.18f, 0f, 0.32f);
        return ColorMath.OklchToHex(nextLightness, nextChroma, hue);
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
