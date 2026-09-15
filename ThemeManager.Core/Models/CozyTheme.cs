using System.Text.Json.Serialization;

namespace ThemeManager.Core.Models;

/// <summary>
/// Represents a full "Cozy" theme: palette, geometry scale, and system-integration prefs.
/// All color values are stored as CSS-style hex strings (#RRGGBB or #AARRGGBB).
/// </summary>
public sealed class CozyTheme
{
    // ── Identity ────────────────────────────────────────────────────────────
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled Theme";
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
    public bool IsBuiltIn { get; set; } = false;

    // ── Palette ─────────────────────────────────────────────────────────────
    public string BackgroundBase { get; set; } = CozyDefaults.Linen;
    public string BackgroundAlt { get; set; } = CozyDefaults.Khaki;
    public string Surface { get; set; } = CozyDefaults.Camel;
    public string AccentPrimary { get; set; } = CozyDefaults.Cocoa;
    public string AccentStrong { get; set; } = CozyDefaults.Espresso;
    public string TextPrimary { get; set; } = CozyDefaults.TextPrimary;
    public string TextMuted { get; set; } = CozyDefaults.TextMuted;
    public string BorderSubtle { get; set; } = CozyDefaults.BorderSubtle;

    // ── Geometry ────────────────────────────────────────────────────────────
    public double CornerRadiusScale { get; set; } = 1.0;
    public double DensityScale { get; set; } = 1.0;

    // ── System integration prefs ─────────────────────────────────────────────
    public bool ApplyToSystemAccent { get; set; } = false;
    public bool ApplyToWallpaper { get; set; } = false;
    public string? WallpaperPath { get; set; } = null;

    // ── Extensibility ────────────────────────────────────────────────────────
    public Dictionary<string, string> CustomTokens { get; set; } = new();

    public CozyTheme Duplicate()
    {
        var clone = (CozyTheme)MemberwiseClone();
        clone.Id = Guid.NewGuid().ToString();
        clone.Name = $"{Name} (Copy)";
        clone.IsBuiltIn = false;
        clone.LastModified = DateTimeOffset.UtcNow;
        clone.CustomTokens = new Dictionary<string, string>(CustomTokens);
        return clone;
    }

    public void ResetToDefault()
    {
        BackgroundBase = CozyDefaults.Linen;
        BackgroundAlt = CozyDefaults.Khaki;
        Surface = CozyDefaults.Camel;
        AccentPrimary = CozyDefaults.Cocoa;
        AccentStrong = CozyDefaults.Espresso;
        TextPrimary = CozyDefaults.TextPrimary;
        TextMuted = CozyDefaults.TextMuted;
        BorderSubtle = CozyDefaults.BorderSubtle;
        CornerRadiusScale = 1.0;
        DensityScale = 1.0;
        LastModified = DateTimeOffset.UtcNow;
    }

    public static string NormalizeHex(string hex)
    {
        hex = hex.TrimStart('#').Trim();
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        if (hex.Length == 8)
            hex = hex[2..];
        return "#" + hex.PadLeft(6, '0').ToUpperInvariant();
    }
}

/// <summary>Canonical high-contrast Cozy Café palette.</summary>
public static class CozyDefaults
{
    public const string Linen = "#F7F3EE";
    public const string Khaki = "#E8DDD1";
    public const string Camel = "#C6AA92";
    public const string Cocoa = "#6A4937";
    public const string Espresso = "#3D2A21";
    public const string TextPrimary = "#241A15";
    public const string TextMuted = "#5D5048";
    public const string BorderSubtle = "#CDBEAE";

    public static CozyTheme CreateDefault() => new()
    {
        Id = "cozy-default",
        Name = "Cozy Café",
        Description = "Warm linen and espresso tones with soft rounded corners.",
        IsBuiltIn = true,
        BackgroundBase = Linen,
        BackgroundAlt = Khaki,
        Surface = Camel,
        AccentPrimary = Cocoa,
        AccentStrong = Espresso,
        TextPrimary = TextPrimary,
        TextMuted = TextMuted,
        BorderSubtle = BorderSubtle,
        CornerRadiusScale = 1.0,
        DensityScale = 1.0,
        ApplyToSystemAccent = false,
        ApplyToWallpaper = false,
    };
}
