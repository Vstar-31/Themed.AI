using System;
using System.Text.Json;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Holds the synchronized state from an active VibeFinder AI WebView2 embed.
/// The React app pushes its playback state via window.chrome.webview.postMessage,
/// which this class parses. When the web embed is active, VibeFinderMeasure will 
/// read from this state instead of polling the backend.
/// </summary>
public static class VibeFinderWebState
{
    public static bool IsActive { get; private set; }

    public static bool IsPlaying { get; private set; }
    public static string Title { get; private set; } = "—";
    public static string Artist { get; private set; } = "—";
    public static string? CoverArt { get; private set; }
    public static string? PreviewUrl { get; private set; }
    public static double CurrentTime { get; private set; }
    public static double Duration { get; private set; }
    public static double Progress => Duration > 0 ? CurrentTime / Duration : 0;
    
    // An action that routes a JSON command string to the active WebView2
    public static Action<string>? SendCommand { get; set; }

    public static void HandleMessage(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "VIBEFINDER_STATE")
            {
                IsActive = true;
                if (root.TryGetProperty("isPlaying", out var playEl)) IsPlaying = playEl.GetBoolean();
                if (root.TryGetProperty("title", out var titleEl)) Title = titleEl.GetString() ?? "—";
                if (root.TryGetProperty("artist", out var artistEl)) Artist = artistEl.GetString() ?? "—";
                if (root.TryGetProperty("coverArt", out var coverEl)) CoverArt = coverEl.GetString();
                if (root.TryGetProperty("previewUrl", out var previewEl)) PreviewUrl = previewEl.GetString();
                if (root.TryGetProperty("currentTime", out var curEl) && curEl.TryGetDouble(out var cTime)) CurrentTime = cTime;
                if (root.TryGetProperty("duration", out var durEl) && durEl.TryGetDouble(out var dur)) Duration = dur;
            }
        }
        catch
        {
            // Ignore malformed messages for security
        }
    }

    public static void Detach()
    {
        IsActive = false;
        SendCommand = null;
    }
}
