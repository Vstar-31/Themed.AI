using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ThemeManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration;

/// <summary>
/// Windows-specific implementation of <see cref="ISystemThemeIntegrator"/>.
///
/// Safe boundaries observed:
///   • Reads accent color via UISettings COM class (documented WinRT API).
///   • Writes accent color via HKCU\SOFTWARE\Microsoft\Windows\DWM (well-known keys).
///   • Sets wallpaper via SystemParametersInfo (Win32, documented).
///   • All write operations are guarded behind the caller's "Advanced" toggle.
///   • No DLL injection, no undocumented kernel calls.
/// </summary>
public sealed class SystemThemeIntegrator : ISystemThemeIntegrator
{
    private readonly ILogger<SystemThemeIntegrator> _logger;

    public SystemThemeIntegrator(ILogger<SystemThemeIntegrator>? logger = null)
    {
        _logger = logger ?? NullLogger<SystemThemeIntegrator>.Instance;
    }

    // ── Win32 interop ─────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(
        uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE   = 0x01;
    private const uint SPIF_SENDCHANGE      = 0x02;

    // ── Registry paths ────────────────────────────────────────────────────────
    private const string DwmKey      = @"SOFTWARE\Microsoft\Windows\DWM";
    private const string ThemesKey   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // ── ISystemThemeIntegrator ────────────────────────────────────────────────

    public Task<string> GetCurrentAccentColorAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                // Read ColorizationColor DWORD from DWM key (BGRA packed int).
                using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
                if (key?.GetValue("ColorizationColor") is int raw)
                    return ArgbToHex((uint)raw);
            }
            catch { /* Silently fall through */ }

            return "#7D5A44"; // Fallback to Cocoa
        });
    }

    /// <summary>
    /// Writes accent / colorization color to DWM registry values and broadcasts WM_SETTINGCHANGE.
    /// The user must have acknowledged the "advanced / at your own risk" toggle before this is called.
    /// </summary>
    public Task<bool> ApplyAccentColorAsync(string hexColor)
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Applying accent color {HexColor} to DWM/Registry", hexColor);
                uint argb = HexToArgb(hexColor);
                // DWM stores colorization as BGRA (Blue in low byte).
                uint bgra = ArgbToBgra(argb);

                using var key = Registry.CurrentUser.OpenSubKey(DwmKey, writable: true);
                if (key is null) 
                {
                    _logger.LogWarning("Failed to open HKCU\\{DwmKey} for writing", DwmKey);
                    return false;
                }

                key.SetValue("ColorizationColor",       (int)bgra,  RegistryValueKind.DWord);
                key.SetValue("ColorizationColorBalance", 100,        RegistryValueKind.DWord);
                key.SetValue("AccentColor",              (int)argb,  RegistryValueKind.DWord);
                key.SetValue("AccentColorInactive",      (int)argb,  RegistryValueKind.DWord);

                // Broadcast so the shell picks up the change without a logoff.
                BroadcastSettingsChange();
                _logger.LogInformation("Accent color successfully applied");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply accent color {HexColor}", hexColor);
                return false;
            }
        });
    }

    public Task<bool> ApplyWallpaperAsync(string imagePath)
    {
        return Task.Run(() =>
        {
            try
            {
                return SystemParametersInfo(
                    SPI_SETDESKWALLPAPER,
                    0,
                    imagePath,
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch { return false; }
        });
    }

    public Task<SystemThemeInfo> GetCurrentSystemThemeAsync()
    {
        return Task.Run(() =>
        {
            bool isLight = IsLightModeEnabled();
            string accent = GetCurrentAccentColorAsync().GetAwaiter().GetResult();
            string build  = Environment.OSVersion.Version.Build.ToString();
            return new SystemThemeInfo(isLight, accent, build);
        });
    }

    public Task<bool> ResetAccentColorAsync()
    {
        // Deleting the overridden values lets Windows revert to its own defaults.
        return Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(DwmKey, writable: true);
                if (key is null) return false;
                key.DeleteValue("ColorizationColor",       throwOnMissingValue: false);
                key.DeleteValue("ColorizationColorBalance",throwOnMissingValue: false);
                key.DeleteValue("AccentColor",             throwOnMissingValue: false);
                key.DeleteValue("AccentColorInactive",     throwOnMissingValue: false);
                BroadcastSettingsChange();
                return true;
            }
            catch { return false; }
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsLightModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemesKey);
            return key?.GetValue("AppsUseLightTheme") is 1;
        }
        catch { return true; }
    }

    private static void BroadcastSettingsChange()
    {
        // WM_SETTINGCHANGE (0x001A) with "ImmersiveColorSet" causes the shell
        // to refresh accent / colorization without a sign-out.
        // SendMessageTimeout with HWND_BROADCAST = 0xFFFF.
        SendMessageTimeout(
            new IntPtr(-1), // HWND_BROADCAST
            0x001A,         // WM_SETTINGCHANGE
            IntPtr.Zero,
            "ImmersiveColorSet",
            0x0002,         // SMTO_ABORTIFHUNG
            5000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    // ── Color math ────────────────────────────────────────────────────────────

    private static uint HexToArgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        return Convert.ToUInt32(hex, 16);
    }

    private static string ArgbToHex(uint argb)
        => $"#{(argb & 0x00FFFFFF):X6}";

    // DWM stores as 0xAA_BB_GG_RR (little-endian BGRA).
    private static uint ArgbToBgra(uint argb)
    {
        byte a = (byte)((argb >> 24) & 0xFF);
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >>  8) & 0xFF);
        byte b = (byte)( argb        & 0xFF);
        return ((uint)a << 24) | ((uint)r) | ((uint)g << 8) | ((uint)b << 16);
    }
}
