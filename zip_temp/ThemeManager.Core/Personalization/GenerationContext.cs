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
    
    // Optionally hold a reference to recently applied themes during this session
    public List<string> SessionHistory { get; set; } = new();
}
