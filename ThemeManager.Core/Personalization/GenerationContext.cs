namespace ThemeManager.Core.Personalization;

public enum Mood
{
    Neutral,
    Focused,
    Relaxed,
    Energetic,
    Cozy,
    Playful,
    Minimal
}

public class GenerationConstraints
{
    public bool MustBeDarkTheme { get; set; }
    public bool MustBeLightTheme { get; set; }
    public bool Minimalist { get; set; }
    public int MaxWidgets { get; set; } = 5;
}

public class GenerationContext
{
    public string Prompt { get; set; } = string.Empty;
    public Mood Mood { get; set; } = Mood.Neutral;
    public GenerationConstraints Constraints { get; set; } = new();

    /// <summary>
    /// Deterministically rotates the preferred candidate treatment: 0 = art-directed base,
    /// 1 = soft-focus alternate, 2 = electric alternate. Incrementing this for Regenerate gives
    /// users a genuinely different result without adding random noise to their prompt.
    /// </summary>
    public int VariationSeed { get; set; }

    // Optionally hold a reference to recently applied themes during this session
    public List<string> SessionHistory { get; set; } = new();
}
