using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>Canonical, modular desktop presets. Each visual element is an independent meter so users can rearrange the composition in the editor.</summary>
public static class DefaultWidgetCatalog
{
    public const string DesignVersion = "3";

    public static void RebuildVibeFinder(SkinDefinition skin)
    {
        if (!skin.Name.StartsWith("VibeFinder", StringComparison.Ordinal)) return;

        var target = skin.Measures.FirstOrDefault(m => m.Type is MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood)?.Target;
        target = string.IsNullOrWhiteSpace(target) ? "$theme" : target;

        skin.Author = "Themed.AI";
        skin.Tags = new List<string> { "built-in", "vibefinder", "music", "desktop", "modular", "album-art" };
        skin.Variables["designVersion"] = DesignVersion;
        skin.ClickThrough = false;
        skin.AlwaysOnTop = true;
        skin.Locked = false;
        skin.DesktopLayer = false;
        skin.Opacity = 0.86;

        switch (skin.Name)
        {
            case "VibeFinder Primary": BuildPrimary(skin, target); break;
            case "VibeFinder Minimal": BuildMinimal(skin, target); break;
            case "VibeFinder Playlist": BuildPlaylist(skin, target); break;
        }
    }

    private static void BuildPrimary(SkinDefinition skin, string target)
    {
        skin.Description = "Compact album-art music dock. Cover, title, artist, mood, progress and transport are separate editable modules.";
        skin.Width = 326;
        skin.Height = 104;
        skin.Meters = new List<MeterDefinition>
        {
            Art(10, 10, 54, 54),
            Text("VibeTitle", 74, 9, 188, 23, 16, true),
            Text("VibeArtist", 74, 31, 188, 18, 11, false),
            Text("VibeMood", 74, 49, 188, 17, 10, false),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 74, Y = 70, Width = 188, Height = 4, BarMax = 100, Opacity = 0.95 },
            Icon("Prev", "\uE100", 211, 79, 24, 20, "themed://media/prev", 13),
            Icon("VibeState", "\uE768", 240, 75, 30, 28, "themed://media/playpause", 15),
            Icon("Next", "\uE101", 275, 79, 24, 20, "themed://media/next", 13),
        };
        skin.Measures = Measures(target);
    }

    private static void BuildMinimal(SkinDefinition skin, string target)
    {
        skin.Description = "Ultra-compact music pill with artwork and transport; every piece remains independently editable.";
        skin.Width = 248;
        skin.Height = 60;
        skin.Meters = new List<MeterDefinition>
        {
            Art(7, 8, 44, 44),
            Text("VibeTitle", 59, 7, 132, 18, 13, true),
            Text("VibeArtist", 59, 25, 132, 15, 10, false),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 59, Y = 45, Width = 132, Height = 3, BarMax = 100, Opacity = 0.9 },
            Icon("Prev", "\uE100", 196, 19, 16, 18, "themed://media/prev", 10),
            Icon("VibeState", "\uE768", 215, 15, 24, 24, "themed://media/playpause", 13),
            Icon("Next", "\uE101", 237, 19, 16, 18, "themed://media/next", 10),
        };
        skin.Measures = Measures(target);
    }

    private static void BuildPlaylist(SkinDefinition skin, string target)
    {
        skin.Description = "Rich but restrained VibeFinder deck with large artwork, mood, progress and transport modules.";
        skin.Width = 360;
        skin.Height = 156;
        skin.Meters = new List<MeterDefinition>
        {
            Art(12, 12, 72, 72),
            Text("VIBE FINDER", 98, 12, 130, 16, 9, true),
            Text("VibeMood", 98, 29, 242, 18, 10, false),
            Text("VibeTitle", 98, 49, 242, 25, 18, true),
            Text("VibeArtist", 98, 74, 242, 18, 11, false),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 98, Y = 98, Width = 242, Height = 4, BarMax = 100, Opacity = 0.95 },
            Text("VibeProgress", 98, 106, 110, 17, 9, false),
            Icon("Prev", "\uE100", 243, 104, 20, 18, "themed://media/prev", 11),
            Icon("VibeState", "\uE768", 266, 100, 28, 26, "themed://media/playpause", 14),
            Icon("Next", "\uE101", 297, 104, 20, 18, "themed://media/next", 11),
        };
        skin.Measures = Measures(target);
    }

    private static List<MeasureDefinition> Measures(string target) => new()
    {
        new() { Name = "VibeTitle", Type = MeasureType.VibeTrackTitle, Target = target },
        new() { Name = "VibeArtist", Type = MeasureType.VibeTrackArtist, Target = target },
        new() { Name = "VibeMood", Type = MeasureType.VibeMood, Target = target },
        new() { Name = "VibeState", Type = MeasureType.VibePlaybackState, Target = target },
        new() { Name = "VibeProgress", Type = MeasureType.VibeTrackProgress, Target = target },
    };

    /// <summary>Album art is a separate visual module bound to the title measure, whose ImageUrl is populated by VibeFinderMeasure.</summary>
    private static MeterDefinition Art(double x, double y, double sizeW, double sizeH)
        => new()
        {
            Kind = MeterKind.Icon,
            MeasureName = "VibeTitle",
            IconGlyph = "\uE7C5",
            X = x,
            Y = y,
            Width = sizeW,
            Height = sizeH,
            FontSize = Math.Max(12, Math.Min(sizeW, sizeH) * 0.38),
            CenterText = true,
            Opacity = 1.0,
        };

    private static MeterDefinition Text(string measure, double x, double y, double width, double height, double font, bool bold)
        => new() { Kind = MeterKind.String, MeasureName = measure, X = x, Y = y, Width = width, Height = height, FontSize = font, Bold = bold, Format = "{1}", Opacity = 1.0 };

    private static MeterDefinition Icon(string measure, string glyph, double x, double y, double width, double height, string action, double fontSize)
        => new() { Kind = MeterKind.Icon, MeasureName = measure, IconGlyph = glyph, X = x, Y = y, Width = width, Height = height, FontSize = fontSize, CenterText = true, ActionUrl = action, Opacity = 1.0 };
}
