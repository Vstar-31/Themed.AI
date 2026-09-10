using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

public static partial class DefaultWidgetCatalog
{
    /// <summary>Compatibility factory for older callers that expect a standalone primary preset.</summary>
    public static SkinDefinition CreateVibeFinderPrimary(string target = "$theme")
        => Create("VibeFinder Primary", target, 50, 50, enabled: true, BuildPrimary);

    /// <summary>Compatibility factory for older callers that expect a standalone minimal preset.</summary>
    public static SkinDefinition CreateVibeFinderMinimal(string target = "$theme")
        => Create("VibeFinder Minimal", target, 50, 200, enabled: false, BuildMinimal);

    /// <summary>Compatibility factory for older callers that expect a standalone playlist preset.</summary>
    public static SkinDefinition CreateVibeFinderPlaylist(string target = "$theme")
        => Create("VibeFinder Playlist", target, 400, 50, enabled: false, BuildPlaylist);

    private static SkinDefinition Create(
        string name,
        string target,
        double x,
        double y,
        bool enabled,
        Action<SkinDefinition, string> builder)
    {
        var skin = new SkinDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Enabled = enabled,
            X = x,
            Y = y,
        };

        builder(skin, string.IsNullOrWhiteSpace(target) ? "$theme" : target);
        skin.Author = "Themed.AI";
        skin.Tags = new List<string> { "built-in", "vibefinder", "music", "desktop", "modular", "album-art" };
        skin.Variables["designVersion"] = DesignVersion;
        skin.ClickThrough = false;
        skin.AlwaysOnTop = false;
        skin.Locked = false;
        skin.DesktopLayer = false;
        skin.Opacity = 0.86;
        return skin;
    }
}
