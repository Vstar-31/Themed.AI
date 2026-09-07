namespace ThemeManager.Core.Models;

/// <summary>Curated built-in theme family. High contrast and restrained surfaces keep typography readable.</summary>
public static class DefaultThemeCatalog
{
    public static IReadOnlyList<CozyTheme> CreateAll() => new[]
    {
        CreateCozyCafe(), CreateMidnightGlass(), CreateAuroraNight(), CreatePaperMono()
    };

    public static CozyTheme CreateCozyCafe() => new()
    {
        Id = "cozy-default", Name = "Cozy Café",
        Description = "Warm linen, espresso and soft clay — calm enough for an all-day desktop.", IsBuiltIn = true,
        BackgroundBase = "#F6F2EC", BackgroundAlt = "#E8DED1", Surface = "#C29F82",
        AccentPrimary = "#7B563F", AccentStrong = "#422E24", TextPrimary = "#2D211A",
        TextMuted = "#6D5C50", BorderSubtle = "#D5C6B7", CornerRadiusScale = 1.15, DensityScale = 1.0
    };

    public static CozyTheme CreateMidnightGlass() => new()
    {
        Id = "midnight-glass", Name = "Midnight Glass",
        Description = "Deep graphite surfaces with electric blue highlights and crisp type.", IsBuiltIn = true,
        BackgroundBase = "#0B0E14", BackgroundAlt = "#121722", Surface = "#1B2433",
        AccentPrimary = "#6EA8FF", AccentStrong = "#D8E6FF", TextPrimary = "#F2F6FC",
        TextMuted = "#AAB7C8", BorderSubtle = "#2A3648", CornerRadiusScale = 1.2, DensityScale = 0.95
    };

    public static CozyTheme CreateAuroraNight() => new()
    {
        Id = "aurora-night", Name = "Aurora Night",
        Description = "A dark atmospheric palette tuned for VibeFinder-driven desktop worlds.", IsBuiltIn = true,
        BackgroundBase = "#090B12", BackgroundAlt = "#111525", Surface = "#1C2034",
        AccentPrimary = "#8C7BFF", AccentStrong = "#5DE0C2", TextPrimary = "#F4F2FF",
        TextMuted = "#B6B5CA", BorderSubtle = "#30344B", CornerRadiusScale = 1.35, DensityScale = 1.0
    };

    public static CozyTheme CreatePaperMono() => new()
    {
        Id = "paper-mono", Name = "Paper Mono",
        Description = "Quiet ivory and ink with a restrained editorial feel.", IsBuiltIn = true,
        BackgroundBase = "#FAFAF7", BackgroundAlt = "#ECECE7", Surface = "#D8D8D0",
        AccentPrimary = "#3F454C", AccentStrong = "#17191C", TextPrimary = "#17191C",
        TextMuted = "#60656B", BorderSubtle = "#D0D0C8", CornerRadiusScale = 0.7, DensityScale = 0.92
    };
}
