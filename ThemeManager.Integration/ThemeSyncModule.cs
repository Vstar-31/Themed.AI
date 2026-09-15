using System.Runtime.CompilerServices;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.Integration;

/// <summary>
/// Bridges Themed.AI theme changes into Windows' native application/system appearance.
/// Registered at module load so every active-theme path gets the same synchronization.
/// </summary>
internal static class ThemeSyncModule
{
    private static readonly SemaphoreSlim ApplyGate = new(1, 1);

    [ModuleInitializer]
    internal static void Initialize()
    {
        ThemeChangeHub.ThemeChanged += OnThemeChanged;
    }

    private static void OnThemeChanged(CozyTheme theme)
    {
        _ = ApplyWindowsThemeAsync(theme);
    }

    private static async Task ApplyWindowsThemeAsync(CozyTheme theme)
    {
        await ApplyGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var integrator = new SystemThemeIntegrator();

            if (theme.ApplyToWindowsApps)
            {
                var isLight = IsLightTheme(theme.BackgroundBase);
                await integrator.ApplyWindowsThemeAsync(isLight).ConfigureAwait(false);
            }

            if (theme.ApplyToSystemAccent)
                await integrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(theme.AccentPrimary)).ConfigureAwait(false);
        }
        catch
        {
            // OS theming is best-effort; a failed registry write must never break theme changes.
        }
        finally
        {
            ApplyGate.Release();
        }
    }

    private static bool IsLightTheme(string hex)
    {
        try
        {
            hex = hex.TrimStart('#').Trim();
            if (hex.Length == 8) hex = hex[2..];
            if (hex.Length == 3)
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            if (hex.Length != 6) return true;

            var r = Convert.ToByte(hex[..2], 16) / 255.0;
            var g = Convert.ToByte(hex.Substring(2, 2), 16) / 255.0;
            var b = Convert.ToByte(hex.Substring(4, 2), 16) / 255.0;

            static double Linear(double c) => c <= 0.04045
                ? c / 12.92
                : Math.Pow((c + 0.055) / 1.055, 2.4);

            var luminance = 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
            return luminance >= 0.55;
        }
        catch
        {
            return true;
        }
    }
}
