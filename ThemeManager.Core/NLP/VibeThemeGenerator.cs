using ThemeManager.Core.Models;

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
