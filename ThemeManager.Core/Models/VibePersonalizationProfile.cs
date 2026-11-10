namespace ThemeManager.Core.Models;

/// <summary>
/// Cross-domain preference state used by the feedback-aware personalization loop.
/// Scores are intentionally domain-neutral so the same profile can influence music
/// recommendation and desktop adaptation.
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
    public string? TopArtist => Artists.Count == 0 ? null : Artists.OrderByDescending(x => x.Value).First().Key;
    public string? TopGenre => Genres.Count == 0 ? null : Genres.OrderByDescending(x => x.Value).First().Key;
    public string? TopTheme => Themes.Count == 0 ? null : Themes.OrderByDescending(x => x.Value).First().Key;
    public string? TopAmbience => Ambience.Count == 0 ? null : Ambience.OrderByDescending(x => x.Value).First().Key;
}
