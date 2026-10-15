using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.Personalization;

public class CandidateEngine
{
    private readonly VibeThemeGenerator _themeGen = new();
    private readonly WidgetVibeGenerator _widgetGen = new();

    public List<ThemeCandidate> GenerateThemeCandidates(GenerationContext context, int count = 3)
    {
        ArgumentNullException.ThrowIfNull(context);
        var candidates = new List<ThemeCandidate>();
        var (baseTheme, baseAnalysis) = _themeGen.GenerateAndExplain(context.Prompt);
        bool? preferredPolarity = context.Constraints.MustBeDarkTheme
            ? true
            : context.Constraints.MustBeLightTheme
                ? false
                : null;
        PaletteQualityValidator.ValidateAndRepair(baseTheme, preferredPolarity);
        candidates.Add(new ThemeCandidate
        {
            Theme = baseTheme,
            Analysis = baseAnalysis is null ? null : AnalysisForVariant(baseAnalysis, baseTheme),
            GenerationSource = "ArtDirectedBase"
        });

        if (count > 1)
        {
            // Create a genuinely different art direction from the same recognised aesthetic rather
            // than appending a generic mood word that can accidentally change the prompt's meaning.
            var softTheme = baseTheme.Duplicate();
            softTheme.Name = $"{baseTheme.Name} · Soft Focus";
            softTheme.Description = $"{baseTheme.Description} A softer, more restrained alternate palette.";
            string softInstruction = context.Constraints.MustBeDarkTheme
                ? "darker and more muted"
                : context.Constraints.MustBeLightTheme
                    ? "lighter and more muted"
                    : context.Mood is Mood.Energetic or Mood.Playful
                        ? "more muted"
                        : "lighter and more muted";
            _themeGen.Refine(softTheme, softInstruction);
            PaletteQualityValidator.ValidateAndRepair(softTheme, preferredPolarity);
            candidates.Add(new ThemeCandidate
            {
                Theme = softTheme,
                Analysis = AnalysisForVariant(baseAnalysis, softTheme),
                GenerationSource = "SoftFocusVariant"
            });
        }

        if (count > 2)
        {
            var electricTheme = baseTheme.Duplicate();
            electricTheme.Name = $"{baseTheme.Name} · Electric Cut";
            electricTheme.Description = $"{baseTheme.Description} A higher-energy alternate with more assertive accents.";
            string electricInstruction = context.Constraints.MustBeDarkTheme
                ? "darker and more vibrant"
                : context.Constraints.MustBeLightTheme
                    ? "lighter and more vibrant"
                    : context.Mood is Mood.Relaxed or Mood.Minimal or Mood.Focused
                        ? "more muted"
                        : "more vibrant";
            _themeGen.Refine(electricTheme, electricInstruction);
            PaletteQualityValidator.ValidateAndRepair(electricTheme, preferredPolarity);
            candidates.Add(new ThemeCandidate
            {
                Theme = electricTheme,
                Analysis = AnalysisForVariant(baseAnalysis, electricTheme),
                GenerationSource = "ElectricCutVariant"
            });
        }

        return candidates;
    }

    private static VibeAnalysisResult AnalysisForVariant(VibeAnalysisResult source, CozyTheme theme)
        => source with
        {
            GeneratedName = theme.Name,
            IsDark = ColorMath.LuminanceFromHex(theme.BackgroundBase) < 0.18f,
            Swatches = [theme.BackgroundBase, theme.BackgroundAlt, theme.Surface, theme.AccentPrimary, theme.AccentStrong]
        };

    public List<WidgetCandidate> GenerateWidgetCandidates(GenerationContext context, double? screenWidth = null, double? screenHeight = null, int count = 3)
    {
        var candidates = new List<WidgetCandidate>();

        // Variant 1: Base prompt
        var (baseWidget, baseAnalysis) = _widgetGen.GenerateAndExplain(context.Prompt, screenWidth, screenHeight);
        candidates.Add(new WidgetCandidate { Skin = baseWidget, Analysis = baseAnalysis, GenerationSource = "BasePrompt" });

        if (count > 1)
        {
            // Variant 2: Minimalist / detailed variant
            string minPrompt = context.Constraints.Minimalist ? $"minimal {context.Prompt}" : $"detailed {context.Prompt}";
            var (minWidget, minAnalysis) = _widgetGen.GenerateAndExplain(minPrompt, screenWidth, screenHeight);
            candidates.Add(new WidgetCandidate { Skin = minWidget, Analysis = minAnalysis, GenerationSource = "StyleInjected" });
        }

        if (count > 2)
        {
            // Variant 3: Bigger, bolder sizing — a genuinely different option from variant 2
            // rather than another wording tweak on the same idea.
            var (boldWidget, boldAnalysis) = _widgetGen.GenerateAndExplain($"big bold {context.Prompt}", screenWidth, screenHeight);
            candidates.Add(new WidgetCandidate { Skin = boldWidget, Analysis = boldAnalysis, GenerationSource = "SizeInjected" });
        }

        // Ideally, we'd alter the prompt based on UserProfile history to generate more variants,
        // but this shows the architectural path.

        return candidates;
    }
}
