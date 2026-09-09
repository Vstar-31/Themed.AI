using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Generates themed wallpapers entirely on-device from the active theme palette.
/// No network calls, paid APIs, API keys, or external image-generation services are required.
/// Pixel output uses a locked bitmap buffer rather than Bitmap.SetPixel so large 1440p/4K
/// wallpapers do not crawl through millions of managed property calls.
/// </summary>
public static class ThemedWallpaperGenerator
{
    private static readonly string[] Styles =
    [
        "Cozy Aurora",
        "Glass Mesh",
        "Orbital Night",
        "Soft Gradient",
        "Zen Minimal",
        "Neon Glow"
    ];

    public static IReadOnlyList<string> AvailableStyles => Styles;

    public static async Task<string> GenerateAsync(
        CozyTheme theme,
        string style,
        int width,
        int height,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        style = AvailableStyles.Contains(style, StringComparer.OrdinalIgnoreCase) ? style : Styles[0];
        width = Math.Clamp(width, 1280, 7680);
        height = Math.Clamp(height, 720, 4320);

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThemedAI", "Wallpapers");
        Directory.CreateDirectory(dir);

        var sourceName = string.IsNullOrWhiteSpace(name) ? theme.Name : name;
        var safeName = string.Concat(sourceName.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Theme";
        var safeStyle = string.Concat(style.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')).Trim();
        var fileName = $"{safeName}-{safeStyle.Replace(' ', '-').ToLowerInvariant()}-{width}x{height}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bmp";
        var path = Path.Combine(dir, fileName);

        await Task.Run(() => RenderBmp(path, theme, style, width, height, cancellationToken), cancellationToken);
        return path;
    }

    private static void RenderBmp(string path, CozyTheme theme, string style, int width, int height, CancellationToken token)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var palette = BuildPalette(theme);
        var normalizedStyle = style.ToLowerInvariant();

        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var buffer = new byte[stride * height];
            var denominatorX = (double)Math.Max(1, width - 1);
            var denominatorY = (double)Math.Max(1, height - 1);

            for (var y = 0; y < height; y++)
            {
                token.ThrowIfCancellationRequested();
                var ny = y / denominatorY;
                var rowOffset = data.Stride >= 0
                    ? y * stride
                    : (height - 1 - y) * stride;

                for (var x = 0; x < width; x++)
                {
                    var nx = x / denominatorX;
                    var color = normalizedStyle switch
                    {
                        "glass mesh" => MeshColor(nx, ny, palette),
                        "orbital night" => OrbitColor(nx, ny, palette),
                        "soft gradient" => GradientColor(nx, ny, palette),
                        "zen minimal" => MinimalColor(nx, ny, palette),
                        "neon glow" => GlowColor(nx, ny, palette),
                        _ => AuroraColor(nx, ny, palette),
                    };

                    var pixel = rowOffset + x * 3;
                    buffer[pixel] = color.B;
                    buffer[pixel + 1] = color.G;
                    buffer[pixel + 2] = color.R;
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        token.ThrowIfCancellationRequested();
        bitmap.Save(path, ImageFormat.Bmp);
    }

    private static Palette BuildPalette(CozyTheme theme) => new(
        Parse(theme.BackgroundBase), Parse(theme.BackgroundAlt), Parse(theme.Surface),
        Parse(theme.AccentPrimary), Parse(theme.AccentStrong), Parse(theme.TextMuted));

    private static Color AuroraColor(double x, double y, Palette p)
    {
        var t = 0.5 + 0.5 * Math.Sin((x * 2.4 + y * 1.2) * Math.PI);
        var glow1 = Blob(x, y, 0.22, 0.28, 0.24);
        var glow2 = Blob(x, y, 0.78, 0.72, 0.30);
        var glow = Math.Clamp(glow1 + glow2 * 0.8, 0, 1);
        return Blend(Blend(p.Base, p.Alt, t * 0.55), p.Accent, glow * 0.55);
    }

    private static Color MeshColor(double x, double y, Palette p)
    {
        var a = 0.5 + 0.5 * Math.Sin((x * 6 + Math.Sin(y * 8)) * Math.PI);
        var b = 0.5 + 0.5 * Math.Sin((y * 5 + x * 2) * Math.PI);
        return Blend(Blend(p.Base, p.Alt, a * 0.35), p.Strong, b * 0.35);
    }

    private static Color OrbitColor(double x, double y, Palette p)
    {
        var dx = x - 0.5;
        var dy = y - 0.5;
        var r = Math.Sqrt(dx * dx + dy * dy);
        var ring = Math.Exp(-Math.Pow((r - 0.24) / 0.045, 2));
        var halo = Math.Exp(-Math.Pow(r / 0.38, 2));
        return Blend(Blend(p.Base, p.Alt, Math.Clamp(1 - r * 1.2, 0, 1) * 0.45), p.Accent, ring * 0.8 + halo * 0.2);
    }

    private static Color GradientColor(double x, double y, Palette p)
    {
        var diagonal = Math.Clamp((x * 0.65 + (1 - y) * 0.35), 0, 1);
        return Blend(Blend(p.Base, p.Alt, diagonal), p.Accent, Math.Pow(diagonal, 2) * 0.45);
    }

    private static Color MinimalColor(double x, double y, Palette p)
    {
        var line = Math.Exp(-Math.Pow((x * 0.82 + y * 0.18 - 0.28) / 0.03, 2));
        var vignette = Math.Clamp(1 - 0.75 * Math.Sqrt(Math.Pow(x - 0.5, 2) + Math.Pow(y - 0.5, 2)), 0, 1);
        return Blend(Blend(p.Base, p.Alt, 0.25 * (1 - vignette)), p.Accent, line * 0.55);
    }

    private static Color GlowColor(double x, double y, Palette p)
    {
        var g1 = Blob(x, y, 0.18, 0.22, 0.30);
        var g2 = Blob(x, y, 0.82, 0.75, 0.26);
        var glow = Math.Clamp(g1 + g2, 0, 1);
        return Blend(p.Base, Blend(p.Alt, p.Accent, 0.65), glow * 0.95);
    }

    private static double Blob(double x, double y, double cx, double cy, double radius)
    {
        var d = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2));
        return Math.Clamp(1 - d / radius, 0, 1);
    }

    private static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }

    private static Color Parse(string hex)
    {
        var value = hex.Trim().TrimStart('#');
        if (value.Length == 3)
            value = $"{value[0]}{value[0]}{value[1]}{value[1]}{value[2]}{value[2]}";
        if (value.Length == 8) value = value[2..];
        if (value.Length != 6) return Color.Black;
        return Color.FromArgb(
            Convert.ToInt32(value[..2], 16),
            Convert.ToInt32(value[2..4], 16),
            Convert.ToInt32(value[4..6], 16));
    }

    private readonly record struct Palette(Color Base, Color Alt, Color Surface, Color Accent, Color Strong, Color Muted);
}
