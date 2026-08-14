using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Personalization;

public class CandidateEngine
{
    private readonly VibeThemeGenerator _themeGen = new();
    private readonly WidgetVibeGenerator _widgetGen = new();

    public List<ThemeCandidate> GenerateThemeCandidates(GenerationContext context, int count = 3)
    {
        var candidates = new List<ThemeCandidate>();

        // Variant 1: Direct prompt
        var baseTheme = _themeGen.Generate(context.Prompt);
        candidates.Add(new ThemeCandidate { Theme = baseTheme, GenerationSource = "BasePrompt" });

        if (count > 1)
        {
            // Variant 2: Inject mood
            string moodPrompt = $"{context.Prompt} {context.Mood.ToString().ToLower()}";
            var moodTheme = _themeGen.Generate(moodPrompt);
            candidates.Add(new ThemeCandidate { Theme = moodTheme, GenerationSource = "MoodInjected" });
        }

        if (count > 2)
        {
            // Variant 3: Inject constraint hints
            string constraintPrompt = context.Constraints.MustBeDarkTheme ? $"{context.Prompt} dark" : 
                                     (context.Constraints.MustBeLightTheme ? $"{context.Prompt} light" : context.Prompt);
            var constraintTheme = _themeGen.Generate(constraintPrompt);
            candidates.Add(new ThemeCandidate { Theme = constraintTheme, GenerationSource = "ConstraintInjected" });
        }

        return candidates;
    }

    public List<WidgetCandidate> GenerateWidgetCandidates(GenerationContext context, double? screenWidth = null, double? screenHeight = null, int count = 3)
    {
        var candidates = new List<WidgetCandidate>();

        // Variant 1: Base prompt
        var baseWidget = _widgetGen.Generate(context.Prompt, screenWidth, screenHeight);
        candidates.Add(new WidgetCandidate { Skin = baseWidget, GenerationSource = "BasePrompt" });

        if (count > 1)
        {
            // Variant 2: Minimalist variant
            string minPrompt = context.Constraints.Minimalist ? $"minimal {context.Prompt}" : $"detailed {context.Prompt}";
            var minWidget = _widgetGen.Generate(minPrompt, screenWidth, screenHeight);
            candidates.Add(new WidgetCandidate { Skin = minWidget, GenerationSource = "StyleInjected" });
        }

        // Ideally, we'd alter the prompt based on UserProfile history to generate more variants,
        // but this shows the architectural path.

        return candidates;
    }
}
