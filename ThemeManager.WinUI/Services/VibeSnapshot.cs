namespace ThemeManager.WinUI.Services;

/// <summary>
/// Lightweight, UI-independent representation of the current VibeFinder mood.
/// Keeping this model small lets desktop scenes react without coupling the scene
/// engine to a particular WebView or media provider.
/// </summary>
public sealed record VibeSnapshot(
    string Mood,
    double Energy,
    double Warmth,
    string? Source = null,
    string? TrackTitle = null,
    string? Artist = null)
{
    public static VibeSnapshot Neutral { get; } = new("Neutral", 0.35, 0.5);
}
