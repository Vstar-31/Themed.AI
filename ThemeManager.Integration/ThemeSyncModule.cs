using System.Globalization;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Integration;

/// <summary>Serializes native appearance changes from the application theme service.</summary>
public sealed class ThemeSyncModule(ISystemThemeIntegrator integrator)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> ApplyAsync(CozyTheme theme)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var applied = true;
            if (theme.ApplyToWindowsApps)
            {
                applied = await integrator.ApplyWindowsThemeAsync(IsLight(theme.BackgroundBase)).ConfigureAwait(false);
            }
            if (theme.ApplyToSystemAccent)
                applied &= await integrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(theme.AccentPrimary)).ConfigureAwait(false);
            return applied;
        }
        catch
        {
            return false;
        }
        finally { _gate.Release(); }
    }

    private static bool IsLight(string background)
    {
        var hex = CozyTheme.NormalizeHex(background);
        if (hex.Length != 7 || !uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _))
            return true;
        return ColorMath.LuminanceFromHex(hex) >= 0.55;
    }
}
