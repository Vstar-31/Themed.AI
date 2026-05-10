namespace ThemeManager.Core.Services;

/// <summary>
/// Abstraction for OS-level theme operations.
/// All methods are async and must be failure-tolerant (never throw to callers).
/// </summary>
public interface ISystemThemeIntegrator
{
    /// <summary>Returns the current Windows accent color as an ARGB hex string.</summary>
    Task<string> GetCurrentAccentColorAsync();

    /// <summary>
    /// Attempts to apply the given hex color as the Windows accent/colorization color.
    /// Operates on well-known DWM registry values only.
    /// Returns false if the operation is not supported or fails.
    /// </summary>
    Task<bool> ApplyAccentColorAsync(string hexColor);

    /// <summary>Sets the desktop wallpaper using the standard SystemParametersInfo API.</summary>
    Task<bool> ApplyWallpaperAsync(string imagePath);

    /// <summary>
    /// Returns high-level info about the current system theme
    /// (Light/Dark mode, accent hex, Windows build).
    /// </summary>
    Task<SystemThemeInfo> GetCurrentSystemThemeAsync();

    /// <summary>Resets colorization registry values to Windows defaults.</summary>
    Task<bool> ResetAccentColorAsync();
}

/// <summary>Read-only snapshot of the current Windows theme state.</summary>
public sealed record SystemThemeInfo(
    bool   IsLightMode,
    string AccentHex,
    string WindowsBuild
);
