using ThemeManager.Core.Models;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// Produces in-between <see cref="CozyTheme"/> color snapshots for a smooth crossfade between two
/// themes. Pure color math with no UI dependency, so it's usable from the WinUI layer's animation
/// loop and testable on its own.
/// </summary>
public static class ThemeInterpolator
{
    /// <summary>
    /// Returns a new theme whose palette is linearly interpolated between <paramref name="from"/>
    /// and <paramref name="to"/> at position <paramref name="t"/> (0 = from, 1 = to). Identity
    /// fields (Id/Name/IsBuiltIn) come from <paramref name="to"/>, so the final frame (t=1) is a
    /// theme that's indistinguishable from just applying <paramref name="to"/> directly.
    /// </summary>
    public static CozyTheme Lerp(CozyTheme from, CozyTheme to, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        return new CozyTheme
        {
            Id        = to.Id,
            Name      = to.Name,
            IsBuiltIn = to.IsBuiltIn,

            BackgroundBase = LerpHex(from.BackgroundBase, to.BackgroundBase, t),
            BackgroundAlt  = LerpHex(from.BackgroundAlt,  to.BackgroundAlt,  t),
            Surface        = LerpHex(from.Surface,        to.Surface,        t),
            AccentPrimary  = LerpHex(from.AccentPrimary,  to.AccentPrimary,  t),
            AccentStrong   = LerpHex(from.AccentStrong,   to.AccentStrong,   t),
            TextPrimary    = LerpHex(from.TextPrimary,    to.TextPrimary,    t),
            TextMuted      = LerpHex(from.TextMuted,      to.TextMuted,      t),
            BorderSubtle   = LerpHex(from.BorderSubtle,   to.BorderSubtle,   t),

            CornerRadiusScale = LerpDouble(from.CornerRadiusScale, to.CornerRadiusScale, t),
            DensityScale      = LerpDouble(from.DensityScale,      to.DensityScale,      t),
        };
    }

    private static double LerpDouble(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Channel-wise RGB lerp between two "#RRGGBB" (or "#AARRGGBB"/"#RGB") hex strings.
    /// Alpha is dropped — every color this app renders is opaque — and malformed input degrades to
    /// black rather than throwing, since this runs once per animation frame and a bad hex string
    /// (e.g. a hand-edited themes.json) shouldn't kill a crossfade mid-flight.</summary>
    private static string LerpHex(string fromHex, string toHex, double t)
    {
        var (r1, g1, b1) = ParseRgb(fromHex);
        var (r2, g2, b2) = ParseRgb(toHex);

        int r = (int)Math.Round(r1 + (r2 - r1) * t);
        int g = (int)Math.Round(g1 + (g2 - g1) * t);
        int b = (int)Math.Round(b1 + (b2 - b1) * t);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static (int r, int g, int b) ParseRgb(string hex)
    {
        hex = hex.TrimStart('#').Trim();
        if (hex.Length == 8) hex = hex[2..]; // AARRGGBB -> RRGGBB
        if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        if (hex.Length != 6) return (0, 0, 0);

        try
        {
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return (r, g, b);
        }
        catch
        {
            return (0, 0, 0);
        }
    }
}
