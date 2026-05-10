namespace ThemeManager.Core.Utilities;

/// <summary>
/// Shared perceptual color mathematics used by multiple pipeline stages.
///
/// Python equivalents:
///   RGB↔HSL  →  colorsys.rgb_to_hls / hls_to_rgb  (note: Python uses HLS order)
///   Luminance →  colour.luminance() or manual sRGB formula
///
/// All methods are static and allocation-free — safe to call on every keystroke.
/// </summary>
public static class ColorMath
{
    // ── Hex parsing ───────────────────────────────────────────────────────────

    /// <summary>Parses "#RRGGBB" or "#RGB" into (r,g,b) in [0,1].</summary>
    public static (float R, float G, float B) HexToRgb(string hex)
    {
        hex = hex.TrimStart('#');

        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

        if (hex.Length != 6)
            return (0f, 0f, 0f);

        return (
            Convert.ToByte(hex[0..2], 16) / 255f,
            Convert.ToByte(hex[2..4], 16) / 255f,
            Convert.ToByte(hex[4..6], 16) / 255f
        );
    }

    /// <summary>Converts (r,g,b) in [0,1] to a "#RRGGBB" hex string.</summary>
    public static string RgbToHex(float r, float g, float b)
    {
        int ri = (int)Math.Round(Math.Clamp(r, 0f, 1f) * 255);
        int gi = (int)Math.Round(Math.Clamp(g, 0f, 1f) * 255);
        int bi = (int)Math.Round(Math.Clamp(b, 0f, 1f) * 255);
        return $"#{ri:X2}{gi:X2}{bi:X2}";
    }

    // ── RGB → HSL ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts (r,g,b) in [0,1] to (hue °, saturation [0,1], lightness [0,1]).
    /// Python: colorsys.rgb_to_hls returns (h, l, s) — note the different order.
    /// </summary>
    public static (float H, float S, float L) RgbToHsl(float r, float g, float b)
    {
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float l   = (max + min) / 2f;
        float s, h;

        if (Math.Abs(max - min) < 1e-6f)
            return (0f, 0f, l); // achromatic

        float d = max - min;
        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

        if      (Math.Abs(max - r) < 1e-6f) h = (g - b) / d + (g < b ? 6f : 0f);
        else if (Math.Abs(max - g) < 1e-6f) h = (b - r) / d + 2f;
        else                                 h = (r - g) / d + 4f;

        h /= 6f; // normalize to [0,1]
        return (h * 360f, s, l);
    }

    /// <summary>Convenience overload that accepts a hex string.</summary>
    public static (float H, float S, float L) HexToHsl(string hex)
    {
        var (r, g, b) = HexToRgb(hex);
        return RgbToHsl(r, g, b);
    }

    // ── HSL → RGB ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts (hue °, saturation [0,1], lightness [0,1]) to (r,g,b) in [0,1].
    /// </summary>
    public static (float R, float G, float B) HslToRgb(float hueDeg, float s, float l)
    {
        if (s < 1e-6f) return (l, l, l); // achromatic

        float h = ((hueDeg % 360f) + 360f) % 360f / 360f;
        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;

        return (HueChannel(p, q, h + 1f/3f),
                HueChannel(p, q, h),
                HueChannel(p, q, h - 1f/3f));
    }

    /// <summary>Convenience overload returning a hex string.</summary>
    public static string HslToHex(float hueDeg, float s, float l)
    {
        var (r, g, b) = HslToRgb(hueDeg, s, l);
        return RgbToHex(r, g, b);
    }

    private static float HueChannel(float p, float q, float t)
    {
        if (t < 0) t += 1f;
        if (t > 1) t -= 1f;
        return t switch
        {
            < 1f/6f => p + (q - p) * 6f * t,
            < 0.5f  => q,
            < 2f/3f => p + (q - p) * (2f/3f - t) * 6f,
            _       => p,
        };
    }

    // ── WCAG Relative Luminance ───────────────────────────────────────────────

    /// <summary>
    /// Computes relative luminance per WCAG 2.1 §1.4.3.
    /// Result is in [0,1]: 0 = absolute black, 1 = absolute white.
    ///
    /// Python equivalent:
    ///   colour.luminance(colour.sRGB_to_XYZ([r,g,b]), 'CIE 1931 2 Degree Standard Observer')
    /// or simply the manual formula below.
    /// </summary>
    public static float RelativeLuminance(float r, float g, float b)
    {
        static float Linearize(float c)
            => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

        float rL = Linearize(r);
        float gL = Linearize(g);
        float bL = Linearize(b);

        return 0.2126f * rL + 0.7152f * gL + 0.0722f * bL;
    }

    /// <summary>Convenience overload that accepts a hex string.</summary>
    public static float LuminanceFromHex(string hex)
    {
        var (r, g, b) = HexToRgb(hex);
        return RelativeLuminance(r, g, b);
    }

    // ── Contrast ratio ────────────────────────────────────────────────────────

    /// <summary>
    /// WCAG contrast ratio between two colors.
    /// Range: 1:1 (identical) to 21:1 (black on white).
    /// AA normal text requires ≥ 4.5, AA large text ≥ 3.0, AAA ≥ 7.0.
    /// </summary>
    public static float ContrastRatio(string hex1, string hex2)
    {
        float l1 = LuminanceFromHex(hex1);
        float l2 = LuminanceFromHex(hex2);
        float lighter = Math.Max(l1, l2);
        float darker  = Math.Min(l1, l2);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    // ── Interpolation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Linearly interpolates between two hex colors in HSL space.
    /// Used for the animated crossfade between themes.
    /// t=0 → colorA, t=1 → colorB.
    /// </summary>
    public static string LerpHex(string hexA, string hexB, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var (ha, sa, la) = HexToHsl(hexA);
        var (hb, sb, lb) = HexToHsl(hexB);

        // Shortest-path hue interpolation.
        float dh = hb - ha;
        if (dh >  180f) dh -= 360f;
        if (dh < -180f) dh += 360f;

        float h = ha + dh * t;
        float s = sa + (sb - sa) * t;
        float l = la + (lb - la) * t;

        return HslToHex(h, s, l);
    }
}
