namespace ThemeManager.Integration.Skins;

public static class YouTubePlaybackState
{
    public static double Progress { get; set; }
    public static bool IsPlaying { get; set; }

    /// <summary>Elapsed playback position, in seconds. 0 when nothing is loaded.</summary>
    public static double CurrentTime { get; set; }

    /// <summary>Total track length, in seconds. 0 when nothing is loaded or duration isn't known yet.</summary>
    public static double Duration { get; set; }
}
