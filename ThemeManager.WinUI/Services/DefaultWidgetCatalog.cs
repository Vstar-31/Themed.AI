using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>Canonical, polished desktop presets. Keeping these in one place makes upgrades deterministic.</summary>
public static class DefaultWidgetCatalog
{
    public const string DesignVersion = "2";

    public static void RebuildVibeFinder(SkinDefinition skin)
    {
        if (!skin.Name.StartsWith("VibeFinder", StringComparison.Ordinal)) return;
        var target = skin.Measures.FirstOrDefault(m => m.Type is MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood)?.Target;
        target = string.IsNullOrWhiteSpace(target) ? "listener|hunter2|$theme" : target;
        skin.Author = "Themed.AI";
        skin.Tags = new List<string> { "built-in", "vibefinder", "music", "desktop" };
        skin.Variables["designVersion"] = DesignVersion;
        skin.ClickThrough = false; skin.AlwaysOnTop = true; skin.Locked = false; skin.DesktopLayer = false; skin.Opacity = 0.84;
        switch (skin.Name)
        {
            case "VibeFinder Primary": BuildPrimary(skin, target); break;
            case "VibeFinder Minimal": BuildMinimal(skin, target); break;
            case "VibeFinder Playlist": BuildPlaylist(skin, target); break;
        }
    }

    private static void BuildPrimary(SkinDefinition skin, string target)
    {
        skin.Description = "A clean VibeFinder player card with track, artist, mood, controls and progress.";
        skin.Width = 380; skin.Height = 156;
        skin.Meters = new List<MeterDefinition>
        {
            Text("VF", 18, 14, 34, 22, 11, true), Text("VibeTitle", 18, 42, 315, 30, 21, true), Text("VibeArtist", 18, 72, 315, 24, 13, false), Text("VibeMood", 18, 98, 315, 22, 12, false),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 18, Y = 128, Width = 270, Height = 6, BarMax = 100 },
            Icon("Prev", "\uE100", 292, 116, 24, 30, "themed://media/prev"), Icon("VibeState", "\uE768", 320, 114, 30, 32, "themed://media/playpause"), Icon("Next", "\uE101", 354, 116, 24, 30, "themed://media/next"),
        };
        skin.Measures = Measures(target);
    }

    private static void BuildMinimal(SkinDefinition skin, string target)
    {
        skin.Description = "A compact always-visible VibeFinder strip for music-first desktops.";
        skin.Width = 300; skin.Height = 72;
        skin.Meters = new List<MeterDefinition>
        {
            Text("VibeTitle", 14, 8, 214, 27, 16, true), Text("VibeArtist", 14, 35, 214, 21, 12, false), Icon("VibeState", "\uE768", 244, 10, 30, 30, "themed://media/playpause"),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 14, Y = 60, Width = 270, Height = 4, BarMax = 100 },
        };
        skin.Measures = Measures(target);
    }

    private static void BuildPlaylist(SkinDefinition skin, string target)
    {
        skin.Description = "A roomy VibeFinder card with rich track metadata and playback controls.";
        skin.Width = 420; skin.Height = 218;
        skin.Meters = new List<MeterDefinition>
        {
            Text("VIBE FINDER", 18, 14, 200, 22, 11, true), Text("VibeMood", 18, 40, 365, 22, 12, false), Text("VibeTitle", 18, 70, 360, 32, 22, true), Text("VibeArtist", 18, 104, 360, 24, 13, false),
            new() { Kind = MeterKind.Ring, MeasureName = "VibeProgress", X = 18, Y = 140, Width = 48, Height = 48, BarMax = 100 }, Text("VibeProgress", 78, 152, 210, 24, 12, false),
            Icon("Prev", "\uE100", 304, 148, 28, 32, "themed://media/prev"), Icon("VibeState", "\uE768", 338, 144, 36, 38, "themed://media/playpause"), Icon("Next", "\uE101", 380, 148, 28, 32, "themed://media/next"),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 18, Y = 201, Width = 384, Height = 5, BarMax = 100 },
        };
        skin.Measures = Measures(target);
    }

    private static List<MeasureDefinition> Measures(string target) => new()
    {
        new() { Name = "VibeTitle", Type = MeasureType.VibeTrackTitle, Target = target }, new() { Name = "VibeArtist", Type = MeasureType.VibeTrackArtist, Target = target },
        new() { Name = "VibeMood", Type = MeasureType.VibeMood, Target = target }, new() { Name = "VibeState", Type = MeasureType.VibePlaybackState, Target = target }, new() { Name = "VibeProgress", Type = MeasureType.VibeTrackProgress, Target = target },
    };

    private static MeterDefinition Text(string measure, double x, double y, double width, double height, double font, bool bold)
        => new() { Kind = MeterKind.String, MeasureName = measure, X = x, Y = y, Width = width, Height = height, FontSize = font, Bold = bold, Format = "{1}", Opacity = 1.0 };

    private static MeterDefinition Icon(string measure, string glyph, double x, double y, double width, double height, string action)
        => new() { Kind = MeterKind.Icon, MeasureName = measure, IconGlyph = glyph, X = x, Y = y, Width = width, Height = height, FontSize = 16, CenterText = true, ActionUrl = action, Opacity = 1.0 };
}
