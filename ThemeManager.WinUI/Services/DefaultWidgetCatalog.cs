using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>Canonical, modular desktop presets. Each visual element is an independent meter so users can rearrange the composition in the editor.</summary>
public static partial class DefaultWidgetCatalog
{
    public const string DesignVersion = "4";

    public static void RebuildVibeFinder(SkinDefinition skin)
    {
        if (!skin.Name.StartsWith("VibeFinder", StringComparison.Ordinal)) return;

        var target = skin.Measures.FirstOrDefault(m => m.Type is MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood)?.Target;
        target = string.IsNullOrWhiteSpace(target) ? "$theme" : target;

        skin.Author = "Themed.AI";
        skin.Tags = new List<string> { "built-in", "vibefinder", "music", "desktop", "modular", "album-art" };
        skin.Variables["designVersion"] = DesignVersion;
        skin.ClickThrough = false;
        skin.AlwaysOnTop = false;
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
            Icon("Prev", "\uE100", 193, 21, 14, 18, "themed://media/prev", 10),
            Icon("VibeState", "\uE768", 210, 17, 20, 24, "themed://media/playpause", 13),
            Icon("Next", "\uE101", 232, 21, 12, 18, "themed://media/next", 10),
        };
        skin.Measures = Measures(target);
    }

    private static void BuildPlaylist(SkinDefinition skin, string target)
    {
        skin.Description = "World-aware VibeFinder deck with album art, now-playing hierarchy, mood, progress and transport modules.";
        skin.Width = 382;
        skin.Height = 170;
        skin.Meters = new List<MeterDefinition>
        {
            Art(12, 12, 82, 82),
            Text("NOW PLAYING", 108, 12, 150, 15, 9, true),
            Text("VibeMood", 108, 28, 250, 18, 10, false),
            Text("VibeTitle", 108, 48, 258, 28, 18, true),
            Text("VibeArtist", 108, 76, 258, 18, 11, false),
            new() { Kind = MeterKind.Bar, MeasureName = "VibeProgress", X = 108, Y = 100, Width = 258, Height = 5, BarMax = 100, Opacity = 0.95 },
            Text("VibeProgress", 108, 109, 110, 17, 9, false),
            Icon("Prev", "\uE100", 254, 106, 20, 18, "themed://media/prev", 11),
            Icon("VibeState", "\uE768", 278, 102, 30, 28, "themed://media/playpause", 15),
            Icon("Next", "\uE101", 312, 106, 20, 18, "themed://media/next", 11),
        };
        skin.Measures = Measures(target);
    }

    /// <summary>
    /// Repairs VibeFinder meters that extend beyond their host window. Existing users may have
    /// an older Minimal layout saved in skins.json; keep all unrelated customization intact.
    /// </summary>
    public static bool RepairVibeFinderLayout(SkinDefinition skin)
    {
        if (!skin.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase))
            return false;

        bool changed = false;

        // The previous default Minimal controls overlapped and the Next button extended 5 DIPs
        // beyond the 248-DIP window. Only migrate this exact stock arrangement; customized layouts
        // are left in place except for the general clipping safety check below.
        if (skin.Name.Equals("VibeFinder Minimal", StringComparison.OrdinalIgnoreCase))
        {
            var prev = skin.Meters.FirstOrDefault(m => m.MeasureName.Equals("Prev", StringComparison.OrdinalIgnoreCase));
            var state = skin.Meters.FirstOrDefault(m => m.MeasureName.Equals("VibeState", StringComparison.OrdinalIgnoreCase));
            var next = skin.Meters.FirstOrDefault(m => m.MeasureName.Equals("Next", StringComparison.OrdinalIgnoreCase));

            bool isLegacyStockLayout =
                prev is not null && state is not null && next is not null &&
                Math.Abs(prev.X - 196) < 0.1 && Math.Abs(prev.Y - 19) < 0.1 && Math.Abs(prev.Width - 16) < 0.1 && Math.Abs(prev.Height - 18) < 0.1 &&
                Math.Abs(state.X - 215) < 0.1 && Math.Abs(state.Y - 15) < 0.1 && Math.Abs(state.Width - 24) < 0.1 && Math.Abs(state.Height - 24) < 0.1 &&
                Math.Abs(next.X - 237) < 0.1 && Math.Abs(next.Y - 19) < 0.1 && Math.Abs(next.Width - 16) < 0.1 && Math.Abs(next.Height - 18) < 0.1;

            if (isLegacyStockLayout)
            {
                prev!.X = 193;
                prev.Y = 21;
                prev.Width = 14;
                state!.X = 210;
                state.Y = 17;
                state.Width = 20;
                next!.X = 232;
                next.Y = 21;
                next.Width = 12;
                changed = true;
            }
        }

        // Guard every VibeFinder variant (including custom layouts) against window-edge clipping.
        // A small inset also accounts for the card's border and scaled window client area.
        const double inset = 4;
        double maxRight = Math.Max(1, skin.Width);
        double maxBottom = Math.Max(1, skin.Height);
        foreach (var meter in skin.Meters)
        {
            if (meter.Width > maxRight - inset)
            {
                skin.Width = meter.Width + inset * 2;
                maxRight = skin.Width;
                changed = true;
            }
            if (meter.Height > maxBottom - inset)
            {
                skin.Height = meter.Height + inset * 2;
                maxBottom = skin.Height;
                changed = true;
            }

            var clampedX = Math.Clamp(meter.X, 0, Math.Max(0, maxRight - meter.Width - inset));
            var clampedY = Math.Clamp(meter.Y, 0, Math.Max(0, maxBottom - meter.Height - inset));
            if (Math.Abs(clampedX - meter.X) > 0.1)
            {
                meter.X = clampedX;
                changed = true;
            }
            if (Math.Abs(clampedY - meter.Y) > 0.1)
            {
                meter.Y = clampedY;
                changed = true;
            }
        }

        return changed;
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
