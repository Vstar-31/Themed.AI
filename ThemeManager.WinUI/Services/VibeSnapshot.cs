using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Lightweight, UI-independent representation of the current VibeFinder mood plus the
/// cross-domain personalization state used by the desktop adaptation layer.
/// </summary>
public sealed record VibeSnapshot(
    string Mood,
    double Energy,
    double Warmth,
    string? Source = null,
    string? TrackTitle = null,
    string? Artist = null,
    VibePersonalizationProfile? Profile = null)
{
    public static VibeSnapshot Neutral { get; } = new("Neutral", 0.35, 0.5, Profile: VibePersonalizationProfile.Empty);
}
