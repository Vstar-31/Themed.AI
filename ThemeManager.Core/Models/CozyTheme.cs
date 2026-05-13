using System.Text.Json.Serialization;

namespace ThemeManager.Core.Models;

/// <summary>
/// Represents a full "Cozy" theme: palette, geometry scale, and system‑integration prefs.
/// All color values are stored as CSS-style hex strings (#RRGGBB or #AARRGGBB).
/// </summary>
public sealed class CozyTheme
{
    // ── Identity ────────────────────────────────────────────────────────────
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = "Untitled Theme";
    public string Description { get; set; } = string.Empty;

    /// <summary>UTC timestamp of last modification, used for ordering.</summary>
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether this theme is built‑in and protected from deletion.</summary>
    public bool IsBuiltIn { get; set; } = false;

    // ── Palette ─────────────────────────────────────────────────────────────
    /// <summary>Main app background (e.g. Linen #F5F1EA).</summary>
    public string BackgroundBase  { get; set; } = CozyDefaults.Linen;

    /// <summary>Secondary surfaces – sidebar, cards (e.g. Khaki #D7C9B8).</summary>
    public string BackgroundAlt   { get; set; } = CozyDefaults.Khaki;

    /// <summary>Interactive controls, filled buttons (e.g. Camel #B2967D).</summary>
    public string Surface         { get; set; } = CozyDefaults.Camel;

    /// <summary>Primary accent, most interactive UI (e.g. Cocoa #7D5A44).</summary>
    public string AccentPrimary   { get; set; } = CozyDefaults.Cocoa;

    /// <summary>Strong emphasis – headers, borders (e.g. Espresso #4A342A).</summary>
    public string AccentStrong    { get; set; } = CozyDefaults.Espresso;

    /// <summary>Default body text color.</summary>
    public string TextPrimary     { get; set; } = "#3B2A20";

    /// <summary>De‑emphasised labels, captions.</summary>
    public string TextMuted       { get; set; } = "#7F7065";

    /// <summary>Subtle dividers and card outlines.</summary>
    public string BorderSubtle    { get; set; } = "#E0D5C7";

    // ── Geometry ────────────────────────────────────────────────────────────
    /// <summary>
    /// Multiplier applied to base corner radii (8/12/16 px).
    /// 1.0 = default; 0.5 = sharper; 1.5 = rounder.
    /// </summary>
    public double CornerRadiusScale { get; set; } = 1.0;

    /// <summary>
    /// Multiplier applied to base spacing unit (8 px).
    /// 1.0 = default; &lt;1 = tighter; &gt;1 = roomier.
    /// </summary>
    public double DensityScale { get; set; } = 1.0;

    // ── System integration prefs ─────────────────────────────────────────────
    public bool    ApplyToSystemAccent { get; set; } = false;
    public bool    ApplyToWallpaper    { get; set; } = false;
    public string? WallpaperPath       { get; set; } = null;

    // ── Extensibility ────────────────────────────────────────────────────────
    /// <summary>Free-form extra tokens for future use (e.g. chart colors, icon set).</summary>
    public Dictionary<string, string> CustomTokens { get; set; } = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns a deep copy of this theme with a new GUID and modified name.</summary>
    public CozyTheme Duplicate()
    {
        var clone = (CozyTheme)MemberwiseClone();
        clone.Id           = Guid.NewGuid().ToString();
        clone.Name         = $"{Name} (Copy)";
        clone.IsBuiltIn    = false;
        clone.LastModified = DateTimeOffset.UtcNow;
        clone.CustomTokens = new Dictionary<string, string>(CustomTokens);
        return clone;
    }

    /// <summary>Overwrites this theme's palette with the Cozy Café defaults.</summary>
    public void ResetToDefault()
    {
        BackgroundBase    = CozyDefaults.Linen;
        BackgroundAlt     = CozyDefaults.Khaki;
        Surface           = CozyDefaults.Camel;
        AccentPrimary     = CozyDefaults.Cocoa;
        AccentStrong      = CozyDefaults.Espresso;
        TextPrimary       = "#3B2A20";
        TextMuted         = "#7F7065";
        BorderSubtle      = "#E0D5C7";
        CornerRadiusScale = 1.0;
        DensityScale      = 1.0;
        LastModified      = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Ensures hex color is always stored as a clean #RRGGBB string (6 chars, uppercase, leading zeros preserved).
    /// </summary>
    public static string NormalizeHex(string hex)
    {
        hex = hex.TrimStart('#').Trim();
        // Expand shorthand #RGB → #RRGGBB
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        // Strip alpha if #AARRGGBB
        if (hex.Length == 8)
            hex = hex[2..];
        // Pad to 6 with leading zeros (fixes the #66298 → #066298 problem)
        return "#" + hex.PadLeft(6, '0').ToUpperInvariant();
    }
}

/// <summary>Canonical hex values for the Cozy Café palette.</summary>
public static class CozyDefaults
{
    public const string Linen    = "#F5F1EA";
    public const string Khaki    = "#D7C9B8";
    public const string Camel    = "#B2967D";
    public const string Cocoa    = "#7D5A44";
    public const string Espresso = "#4A342A";

    /// <summary>Returns the fully-initialized default "Cozy Café" theme.</summary>
    public static CozyTheme CreateDefault() => new()
    {
        Id           = "cozy-default",
        Name         = "Cozy Café",
        Description  = "Warm linen and espresso tones with soft rounded corners.",
        IsBuiltIn    = true,
        BackgroundBase  = Linen,
        BackgroundAlt   = Khaki,
        Surface         = Camel,
        AccentPrimary   = Cocoa,
        AccentStrong    = Espresso,
        TextPrimary     = "#3B2A20",
        TextMuted       = "#7F7065",
        BorderSubtle    = "#E0D5C7",
        CornerRadiusScale = 1.0,
        DensityScale      = 1.0,
        ApplyToSystemAccent = false,
        ApplyToWallpaper    = false,
    };
}
