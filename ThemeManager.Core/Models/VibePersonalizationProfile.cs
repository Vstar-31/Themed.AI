namespace ThemeManager.Core.Models;

/// <summary>
/// Cross-domain preference state received from VibeFinderAI. Scores are intentionally generic so
/// music and interface preferences can be learned by the same personalization loop.
/// </summary>
public sealed record VibePersonalizationProfile(
    double Signals,
    IReadOnlyDictionary<string, double> Moods,
    IReadOnlyDictionary<string, double> Artists,
    IReadOnlyDictionary<string, double> Genres,
    IReadOnlyDictionary<string, double> Themes,
    IReadOnlyDictionary<string, double> Ambience,
    IReadOnlyList<string> RecentContexts)
{
    public static VibePersonalizationProfile Empty { get; } = new(
        0,
        new Dictionary<string, double>(),
        new Dictionary<string, double>(),
        new Dictionary<string, double>(),
        new Dictionary<string, double>(),
        new Dictionary<string, double>(),
        Array.Empty<string>());

    public string? TopMood => Moods.Count == 0 ? null : Moods.OrderByDescending(x => x.Value).First().Key;
    public string? TopTheme => Themes.Count == 0 ? null : Themes.OrderByDescending(x => x.Value).First().Key;
}
