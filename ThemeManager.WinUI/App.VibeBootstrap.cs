using ThemeManager.Integration.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI;

/// <summary>
/// Connects the existing VibeFinder WebView state to the new desktop-world signal bus.
/// The bridge is intentionally tiny: VibeFinder remains responsible for playback/data,
/// while the scene layer only consumes a normalized snapshot.
/// </summary>
public partial class App
{
    private static readonly bool VibeBridgeInitialized = InitializeVibeBridge();

    private static bool InitializeVibeBridge()
    {
        VibeFinderWebState.StateChanged += OnVibeFinderStateChanged;
        PublishVibeSnapshot();
        return true;
    }

    private static void OnVibeFinderStateChanged(object? sender, EventArgs e) => PublishVibeSnapshot();

    private static void PublishVibeSnapshot()
    {
        var active = VibeFinderWebState.IsActive;
        var playing = VibeFinderWebState.IsPlaying;
        var mood = playing ? "Now Playing" : active ? "Discovery" : "Neutral";
        var energy = playing ? 0.82 : active ? 0.48 : 0.35;

        VibeSnapshotHub.Publish(new VibeSnapshot(
            Mood: mood,
            Energy: energy,
            Warmth: 0.5,
            Source: "VibeFinderAI",
            TrackTitle: active ? VibeFinderWebState.Title : null,
            Artist: active ? VibeFinderWebState.Artist : null));
    }
}
