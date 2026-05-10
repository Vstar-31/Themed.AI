using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Integration;
using Windows.UI;

namespace ThemeManager.WinUI;

/// <summary>
/// Application entry point.
/// Bootstraps all services, wires up the live-theming pipeline, and launches the main window.
/// </summary>
public partial class App : Application
{
    // ── Public service accessors (poor-man's DI; easy to replace with DI container) ──
    public static ThemeService    ThemeService    { get; private set; } = null!;
    public static ThemeRepository ThemeRepository { get; private set; } = null!;
    public static ISystemThemeIntegrator SystemIntegrator { get; private set; } = null!;

    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        ThemeRepository  = new ThemeRepository();
        ThemeService     = new ThemeService(ThemeRepository);
        SystemIntegrator = new SystemThemeIntegrator();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialise (loads JSON, sets Cozy Café as active) before the window appears.
        await ThemeService.InitializeAsync();

        // Apply the loaded theme to ResourceDictionary immediately.
        ApplyThemeToResources(ThemeService.ActiveTheme);

        // Subscribe to future theme changes for live updating.
        ThemeService.ThemeChanged += (_, theme) => ApplyThemeToResources(theme);

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }

    // ── Live theming ──────────────────────────────────────────────────────────

    /// <summary>
    /// Translates a <see cref="CozyTheme"/> into XAML resource entries and overwrites
    /// the running ResourceDictionary so every bound control updates immediately.
    /// Must be called on the UI thread.
    /// </summary>
    public static void ApplyThemeToResources(CozyTheme theme)
    {
        var resources = Current.Resources;

        // Helper: parse hex → Windows.UI.Color → SolidColorBrush.
        SolidColorBrush Brush(string hex) => new(HexToColor(hex));
        Color Col(string hex) => HexToColor(hex);

        // Colors
        resources["ColorBackgroundBase"]  = Col(theme.BackgroundBase);
        resources["ColorBackgroundAlt"]   = Col(theme.BackgroundAlt);
        resources["ColorSurface"]         = Col(theme.Surface);
        resources["ColorAccentPrimary"]   = Col(theme.AccentPrimary);
        resources["ColorAccentStrong"]    = Col(theme.AccentStrong);
        resources["ColorTextPrimary"]     = Col(theme.TextPrimary);
        resources["ColorTextMuted"]       = Col(theme.TextMuted);
        resources["ColorBorderSubtle"]    = Col(theme.BorderSubtle);

        // Brushes (the binding targets throughout the visual tree)
        resources["AppBackgroundBrush"]     = Brush(theme.BackgroundBase);
        resources["SidebarBackgroundBrush"] = Brush(theme.BackgroundAlt);
        resources["CardBackgroundBrush"]    = Brush(theme.BackgroundBase);
        resources["SurfaceBrush"]           = Brush(theme.Surface);
        resources["PrimaryAccentBrush"]     = Brush(theme.AccentPrimary);
        resources["StrongAccentBrush"]      = Brush(theme.AccentStrong);
        resources["TextPrimaryBrush"]       = Brush(theme.TextPrimary);
        resources["TextMutedBrush"]         = Brush(theme.TextMuted);
        resources["BorderSubtleBrush"]      = Brush(theme.BorderSubtle);

        // Hover/pressed variants (auto-computed from base surface color).
        resources["SurfaceHoverBrush"]    = Brush(LightenHex(theme.Surface, 0.15));
        resources["SurfacePressedBrush"]  = Brush(DarkenHex (theme.Surface, 0.10));

        // Corner radius tokens (scaled by theme preference)
        double s = Math.Clamp(theme.CornerRadiusScale, 0.25, 2.0);
        resources["CornerRadiusLarge"]  = new CornerRadius(16 * s);
        resources["CornerRadiusMedium"] = new CornerRadius(12 * s);
        resources["CornerRadiusSmall"]  = new CornerRadius( 8 * s);

        // Spacing tokens (scaled by density)
        double d = Math.Clamp(theme.DensityScale, 0.5, 2.0);
        resources["Space1"] = 4  * d;
        resources["Space2"] = 8  * d;
        resources["Space3"] = 12 * d;
        resources["Space4"] = 16 * d;
        resources["Space5"] = 24 * d;
        resources["Space6"] = 32 * d;
        resources["Space7"] = 48 * d;
    }

    // ── Color utilities ───────────────────────────────────────────────────────

    public static Color HexToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length switch
        {
            6 => Color.FromArgb(0xFF,
                     Convert.ToByte(hex[..2], 16),
                     Convert.ToByte(hex[2..4], 16),
                     Convert.ToByte(hex[4..6], 16)),
            8 => Color.FromArgb(
                     Convert.ToByte(hex[..2], 16),
                     Convert.ToByte(hex[2..4], 16),
                     Convert.ToByte(hex[4..6], 16),
                     Convert.ToByte(hex[6..8], 16)),
            _ => Color.FromArgb(0xFF, 0x7D, 0x5A, 0x44) // Cocoa fallback
        };
    }

    public static string ColorToHex(Color c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string LightenHex(string hex, double amount)
    {
        var c = HexToColor(hex);
        return ColorToHex(Color.FromArgb(c.A,
            (byte)Math.Min(255, c.R + 255 * amount),
            (byte)Math.Min(255, c.G + 255 * amount),
            (byte)Math.Min(255, c.B + 255 * amount)));
    }

    private static string DarkenHex(string hex, double amount)
    {
        var c = HexToColor(hex);
        return ColorToHex(Color.FromArgb(c.A,
            (byte)Math.Max(0, c.R - 255 * amount),
            (byte)Math.Max(0, c.G - 255 * amount),
            (byte)Math.Max(0, c.B - 255 * amount)));
    }
}
