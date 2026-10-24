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

    // ── OKLCH (perceptual hue/chroma/lightness) ───────────────────────────────

    /// <summary>
    /// Converts an sRGB hex color to OKLCH. Unlike HSL, OKLCH's lightness is designed to track
    /// perceived brightness more consistently across hues; it is used by the free-form palette
    /// composer to avoid lumpy ramps between blue, yellow, red and other color families.
    /// Returns (lightness 0..1, chroma 0..~0.4, hue degrees).
    /// </summary>
    public static (float L, float C, float H) HexToOklch(string hex)
    {
        var (r8, g8, b8) = HexToRgb(hex);
        static float Linearize(float c)
            => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

        float r = Linearize(r8), g = Linearize(g8), b = Linearize(b8);
        float l = MathF.Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
        float m = MathF.Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
        float s = MathF.Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);

        float L = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
        float a = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
        float bb = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;
        float C = MathF.Sqrt(a * a + bb * bb);
        float H = C < 1e-7f ? 0f : MathF.Atan2(bb, a) * 180f / MathF.PI;
        if (H < 0) H += 360f;
        return (Math.Clamp(L, 0f, 1f), C, H);
    }

    /// <summary>
    /// Converts OKLCH to sRGB hex with chroma-reduction gamut mapping. It preserves the requested
    /// perceptual lightness/hue as far as the sRGB gamut allows, rather than clipping channels and
    /// unexpectedly shifting neon hues at the edges of the displayable color space.
    /// </summary>
    public static string OklchToHex(float lightness, float chroma, float hueDeg)
    {
        lightness = Math.Clamp(lightness, 0f, 1f);
        chroma = Math.Clamp(chroma, 0f, 0.5f);
        hueDeg = ((hueDeg % 360f) + 360f) % 360f;

        static bool InSrgbGamut((float R, float G, float B) rgb)
        {
            // Matrix operations at saturated gamut edges can overshoot by a few millionths due to
            // floating-point error. Treat that as in-gamut; channels are clamped only at encoding.
            const float epsilon = 0.000005f;
            return rgb.R >= -epsilon && rgb.R <= 1f + epsilon &&
                   rgb.G >= -epsilon && rgb.G <= 1f + epsilon &&
                   rgb.B >= -epsilon && rgb.B <= 1f + epsilon;
        }

        static string EncodeHex((float R, float G, float B) rgb)
        {
            static float Encode(float linear)
            {
                linear = Math.Clamp(linear, 0f, 1f);
                return linear <= 0.0031308f
                    ? 12.92f * linear
                    : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
            }

            return RgbToHex(Encode(rgb.R), Encode(rgb.G), Encode(rgb.B));
        }

        var requested = OklchToLinearRgb(lightness, chroma, hueDeg);
        if (InSrgbGamut(requested))
            return EncodeHex(requested);

        // Reducing chroma by 10% repeatedly was too coarse: a tiny negative channel at pure
        // saturated red/green/blue could discard a large amount of chroma and visibly rotate the
        // result. Binary-search the highest in-gamut chroma instead, preserving requested OKLCH
        // lightness and hue as closely as the sRGB boundary allows.
        float low = 0f;
        float high = chroma;
        for (int i = 0; i < 28; i++)
        {
            float mid = (low + high) * 0.5f;
            if (InSrgbGamut(OklchToLinearRgb(lightness, mid, hueDeg)))
                low = mid;
            else
                high = mid;
        }

        var mapped = OklchToLinearRgb(lightness, low, hueDeg);
        return EncodeHex(mapped);
    }

    private static (float R, float G, float B) OklchToLinearRgb(float lightness, float chroma, float hueDeg)
    {
        float radians = hueDeg * MathF.PI / 180f;
        float a = chroma * MathF.Cos(radians);
        float b = chroma * MathF.Sin(radians);

        float l_ = lightness + 0.3963377774f * a + 0.2158037573f * b;
        float m_ = lightness - 0.1055613458f * a - 0.0638541728f * b;
        float s_ = lightness - 0.0894841775f * a - 1.2914855480f * b;

        float l = l_ * l_ * l_;
        float m = m_ * m_ * m_;
        float s = s_ * s_ * s_;

        float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        float blue = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
        return (r, g, blue);
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
