namespace ThemeManager.Core.Models;

/// <summary>
/// A complete visual desktop state. Scenes sit above themes and widgets and become the future unit
/// for generation, sharing, scheduling and VibeFinder adaptation.
/// </summary>
public sealed class DesktopScene
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled Scene";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "Themed.AI";
    public List<string> Tags { get; set; } = new();
    public string ThemeId { get; set; } = "";
    public string? WallpaperPath { get; set; }
    public double WallpaperOpacity { get; set; } = 1.0;
    public string WallpaperFit { get; set; } = "Fill";
    public List<SceneWidgetPlacement> Widgets { get; set; } = new();
    public List<SceneEffect> Effects { get; set; } = new();
    public SceneBehavior Behavior { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public sealed class SceneWidgetPlacement
{
    public string WidgetId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1.0;
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public string Monitor { get; set; } = "Primary";
}

public sealed class SceneEffect
{
    public string Type { get; set; } = "Glow";
    public double Intensity { get; set; } = 0.5;
    public double Speed { get; set; } = 1.0;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SceneBehavior
{
    public bool FollowWindowsTheme { get; set; }
    public bool ReactToMedia { get; set; } = true;
    public bool ReactToVibeFinder { get; set; } = true;
    public bool ReactToWeather { get; set; }
    public bool AutoSwitch { get; set; }
    public double TransitionSeconds { get; set; } = 0.8;
    public double MotionIntensity { get; set; } = 0.35;
    public double AudioSensitivity { get; set; } = 0.65;
}
