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
        var profiles = AestheticProfileCatalog.FindAll(vibeText);
        var profile = profiles.FirstOrDefault();
        var secondaryProfile = AestheticProfileCatalog.RequestsBlend(vibeText, profiles) && profiles.Count > 1
            ? profiles[1]
            : null;

        CozyTheme theme;
        if (profile is not null && secondaryProfile is not null)
        {
            theme = AestheticProfileCatalog.CreateBlendedTheme(profile, secondaryProfile, vibeText);
        }
        else if (profile is not null)
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

            // Free-form prompts need the same finishing language as named profiles: glass-like
            // surfaces, minimal density, maximalist accents, specific accent requests, etc.
            // This shared composition pass lets natural descriptions refine the palette without
            // requiring every phrase to be a pre-authored aesthetic name.
            AestheticProfileCatalog.ApplyPromptModifiers(theme, vibeText);
        }

        // One final quality gate covers every route: exact profile, aesthetic blend, and free-form
        // color-language generation. This is intentionally at the public boundary so a future
        // candidate strategy cannot bypass accessibility or tonal-hierarchy repair.
        bool? requestedPolarity = profile is null && signal.HasSignal ? signal.IsDark : null;
        var finalQuality = PaletteQualityValidator.ValidateAndRepair(theme, requestedPolarity);
        if (!finalQuality.PassesAA)
            throw new InvalidOperationException(
                $"Generated palette failed its final accessibility gate ({finalQuality.PassingPairs}/{finalQuality.TotalPairs} pairs).");

        var analysis = BuildAnalysis(signal, theme, profile, secondaryProfile);
        return (theme, analysis);
    }

    private static VibeAnalysisResult BuildAnalysis(
        VibeSignal signal,
        CozyTheme theme,
        AestheticProfileCatalog.Profile? profile,
        AestheticProfileCatalog.Profile? secondaryProfile)
    {
        var keywords = signal.MatchedKeywords.ToList();
        var categories = new Dictionary<string, string>(signal.KeywordCategories, StringComparer.OrdinalIgnoreCase);
        var bigrams = signal.BigramMatches.ToList();
        var fuzzy = new Dictionary<string, string>(signal.FuzzyCorrections, StringComparer.OrdinalIgnoreCase);

        if (profile is not null)
        {
            // Surface detected art direction in the insight panel even when a name is not a
            // traditional color-lexicon term. For blends, retain both source aesthetics so the UI
            // can explain how the generated visual language was composed.
            if (!keywords.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
                keywords.Insert(0, profile.Name);
            categories[profile.Name] = "aesthetic profile";
        }

        if (secondaryProfile is not null)
        {
            if (!keywords.Contains(secondaryProfile.Name, StringComparer.OrdinalIgnoreCase))
                keywords.Insert(Math.Min(1, keywords.Count), secondaryProfile.Name);
            categories[secondaryProfile.Name] = "blended aesthetic";
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
    private static readonly string[] RefinementCues =
    [
        "darker", "make it darker", "darken", "less bright", "dim it", "dimmer",
        "lighter", "make it lighter", "lighten", "brighter", "make it brighter", "brighten it",
        "more muted", "muted", "desaturated", "less saturated", "soften the colors", "softer",
        "subtle", "faded", "matte", "pastel", "quiet luxury", "glassy",
        "more vibrant", "vibrant", "vivid", "more saturated", "punchier", "electric", "extra neon",
        "dopamine", "colorful", "bold", "maximalist", "camp", "brat", "hyperpop",
        "warm", "warmer", "more warm", "cozy", "cozier", "warmth", "amber glow",
        "cool", "cooler", "more cool", "cold", "colder", "make it icy", "frosted", "glacier",
        "more red", "make it red", "more orange", "make it orange", "more gold", "make it gold",
        "more yellow", "make it yellow", "more green", "make it green", "more teal", "make it teal",
        "more cyan", "make it cyan", "more blue", "make it blue", "more indigo", "make it indigo",
        "more purple", "make it purple", "more magenta", "make it magenta", "more pink", "make it pink",
        "red accents", "orange accents", "gold accents", "yellow accents", "green accents", "teal accents",
        "cyan accents", "blue accents", "indigo accents", "purple accents", "violet accents",
        "magenta accents", "pink accents", "make accents red", "make accents orange", "make accents gold",
        "make accents yellow", "make accents green", "make accents teal", "make accents cyan",
        "make accents blue", "make accents indigo", "make accents purple", "make accents violet",
        "make the accents violet", "more violet accents", "more purple accents", "more violet", "more red accents",
        "more orange accents", "more gold accents", "more yellow accents", "more green accents",
        "more teal accents", "more cyan accents", "more blue accents", "more indigo accents",
        "more magenta accents", "more pink accents", "make it violet", "in violet",
        "glass", "glassmorphism", "liquid glass", "frutiger aero",
        "holographic", "glossy", "chrome", "brutalist", "hard edges", "industrial", "raw concrete",
        "terminal ui", "minimal", "minimalist", "clean girl", "less clutter", "focus desk",
        "spacious", "airy layout", "breathing room", "lots of whitespace", "eclectic", "playful maximalism"
    ];

    /// <summary>
    /// Distinguishes an imperative edit to the current result from a fresh aesthetic prompt.
    /// A profile name such as "Dark Academia" or "Frutiger Aero glass ocean" must generate a new
    /// palette even if the user already has a result; commands such as "make it darker" or
    /// "add violet accents" refine that existing result.
    /// </summary>
    public bool IsRefinementInstruction(string? instructionText)
    {
        var normalized = AestheticProfileCatalog.NormalizeForMatching(instructionText);
        if (normalized.Length == 0) return false;

        // Explicit selection language always creates a new result when a named aesthetic is
        // present. Otherwise "change the theme to Frutiger Aero" can be mistaken for a glass
        // refinement simply because the profile name contains one of the catalog's modifier cues.
        bool namesAesthetic = AestheticProfileCatalog.FindAll(instructionText).Count > 0;
        bool explicitStyleSwitch =
            normalized.StartsWith("change theme to ", StringComparison.Ordinal) ||
            normalized.StartsWith("change the theme to ", StringComparison.Ordinal) ||
            normalized.StartsWith("switch to ", StringComparison.Ordinal) ||
            normalized.StartsWith("use the ", StringComparison.Ordinal) ||
            normalized.StartsWith("use ", StringComparison.Ordinal) ||
            normalized.StartsWith("generate ", StringComparison.Ordinal) ||
            normalized.StartsWith("create ", StringComparison.Ordinal) ||
            normalized.StartsWith("make me a ", StringComparison.Ordinal);

        if (namesAesthetic && explicitStyleSwitch)
            return false;

        // A verb alone is not enough: "change theme to Dark Academia" is a fresh aesthetic
        // request, while "change the palette to more violet" is an edit. Require both an edit-like
        // lead-in and a recognized color/composition cue before reusing the current result.
        bool hasSupportedCue = RefinementCues.Any(cue =>
            AestheticProfileCatalog.ContainsPositiveCue(instructionText, cue));
        if (!hasSupportedCue) return false;

        string[] commandPrefixes =
        [
            "make it ", "make this ", "make the ", "please make it ", "change ", "adjust ", "refine ", "tweak ", "add ", "remove ", "use ",
            "tone down ", "turn up ", "increase ", "decrease ", "shift ", "move the ", "give it ",
            "less ", "more ", "darker", "lighter", "brighter", "darken", "lighten", "brighten",
            "dim it", "dimmer", "vibrant", "muted", "desaturated", "warmer", "cooler", "softer",
            "pastel", "matte", "bold", "bolder"
        ];

        return commandPrefixes.Any(prefix =>
            normalized.Equals(prefix.Trim(), StringComparison.Ordinal) ||
            normalized.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Applies conversational refinements through the same semantic and compositional path used
    /// for initial generation. Color requests, negative phrasing, aesthetic materials, and geometry
    /// changes therefore behave consistently whether the user starts from scratch or iterates.
    /// </summary>
    public CozyTheme Refine(CozyTheme baseTheme, string instructionText)
    {
        ArgumentNullException.ThrowIfNull(baseTheme);
        if (string.IsNullOrWhiteSpace(instructionText))
            return baseTheme;

        bool hasSupportedIntent = RefinementCues.Any(cue =>
            AestheticProfileCatalog.ContainsPositiveCue(instructionText, cue));
        if (!hasSupportedIntent)
            return baseTheme; // Never guess at an instruction the NLP layer doesn't recognize.

        // Refinement should improve readability but retain the palette's original light/dark mode
        // unless the user explicitly regenerates the theme in another mode.
        bool preferDark = ColorMath.LuminanceFromHex(baseTheme.BackgroundBase) < 0.30f;
        AestheticProfileCatalog.ApplyPromptModifiers(baseTheme, instructionText);
        var quality = PaletteQualityValidator.ValidateAndRepair(baseTheme, preferDark);

        if (!quality.PassesAA)
            throw new InvalidOperationException(
                $"Refined palette failed its final accessibility gate ({quality.PassingPairs}/{quality.TotalPairs} pairs).");

        return baseTheme;
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
