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
///   • Reads accent color via HKCU DWM registry key (documented).
///   • Writes accent color via HKCU\SOFTWARE\Microsoft\Windows\DWM and Explorer\Accent.
///   • Sets wallpaper via SystemParametersInfo (Win32, documented).
///   • No DLL injection, no undocumented kernel calls.
/// </summary>
public sealed class SystemThemeIntegrator : ISystemThemeIntegrator
{
    private readonly ILogger _logger;

    public SystemThemeIntegrator(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    // ── Win32 interop ──────────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(
        uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE   = 0x01;
    private const uint SPIF_SENDCHANGE      = 0x02;

    // ── Registry paths ──────────────────────────────────────────────────────────────

    private const string DwmKey        = @"SOFTWARE\Microsoft\Windows\DWM";
    private const string ThemesKey     = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ExplorerKey   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string ControlColors = @"Control Panel\Colors";

    // ── ISystemThemeIntegrator ───────────────────────────────────────────────────────────

    public Task<string> GetCurrentAccentColorAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
                if (key?.GetValue("ColorizationColor") is int raw)
                    return ArgbToHex((uint)raw);
            }
            catch { /* Silently fall through */ }
            return "#7D5A44";
        });
    }

    /// <summary>
    /// Writes the accent color to all required registry locations and broadcasts
    /// WM_SETTINGCHANGE so Windows 11 picks up the change immediately.
    ///
    /// Keys written:
    ///   HKCU\SOFTWARE\Microsoft\Windows\DWM              — ColorizationColor (ARGB), AccentColor, ColorPrevalence
    ///   HKCU\...\Themes\Personalize                      — ColorPrevalence only
    ///   HKCU\...\Explorer\Accent                         — AccentPalette blob, StartColorMenu/AccentColorMenu (ABGR)
    ///   HKCU\Control Panel\Colors                        — Hilight + HotTrackingColor (decimal R G B) for shell repaint
    /// </summary>
    public Task<bool> ApplyAccentColorAsync(string hexColor)
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Applying accent color {HexColor} to DWM/Registry", hexColor);

                uint argb = HexToArgb(hexColor);
                byte r = (byte)((argb >> 16) & 0xFF);
                byte g = (byte)((argb >> 8)  & 0xFF);
                byte b = (byte)( argb        & 0xFF);

                // Explorer\Accent uses ABGR (0xFFBBGGRR) — NOT ARGB.
                uint abgr = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;

                // ── 1. DWM key (ARGB) ────────────────────────────────────────────────
                using (var dwmKey = Registry.CurrentUser.OpenSubKey(DwmKey, writable: true))
                {
                    if (dwmKey is null)
                    {
                        _logger.LogWarning("Failed to open HKCU\\{DwmKey} for writing", DwmKey);
                        return false;
                    }

                    dwmKey.SetValue("ColorizationColor",        (int)argb, RegistryValueKind.DWord);
                    dwmKey.SetValue("ColorizationColorBalance", 100,        RegistryValueKind.DWord);
                    dwmKey.SetValue("AccentColor",              (int)argb, RegistryValueKind.DWord);
                    dwmKey.SetValue("AccentColorInactive",      (int)argb, RegistryValueKind.DWord);
                    dwmKey.SetValue("ColorPrevalence",          1,          RegistryValueKind.DWord);
                    dwmKey.Flush();
                }

                // ── 2. Themes\Personalize (ColorPrevalence only — never touch light/dark mode) ──
                using (var pKey = Registry.CurrentUser.OpenSubKey(ThemesKey, writable: true))
                {
                    if (pKey != null)
                    {
                        pKey.SetValue("ColorPrevalence", 1, RegistryValueKind.DWord);
                        pKey.Flush();
                    }
                }

                // ── 3. Explorer\Accent (ABGR byte order) ──────────────────────────
                using (var accentKey = Registry.CurrentUser.CreateSubKey(ExplorerKey))
                {
                    if (accentKey != null)
                    {
                        byte[] palette = BuildAccentPalette(r, g, b);
                        accentKey.SetValue("AccentPalette",   palette,   RegistryValueKind.Binary);
                        accentKey.SetValue("StartColorMenu",  (int)abgr, RegistryValueKind.DWord);
                        accentKey.SetValue("AccentColorMenu", (int)abgr, RegistryValueKind.DWord);
                        accentKey.Flush();
                    }
                }

                // ── 4. Control Panel\Colors (decimal "R G B" strings) ────────────────
                // Win11 shell reads Hilight and HotTrackingColor from here for
                // selection highlights and taskbar hover states. Must be "R G B"
                // decimal strings, e.g. "121 83 82".
                string rgbDecimal = $"{r} {g} {b}";
                using (var cpKey = Registry.CurrentUser.OpenSubKey(ControlColors, writable: true))
                {
                    if (cpKey != null)
                    {
                        cpKey.SetValue("Hilight",          rgbDecimal, RegistryValueKind.String);
                        cpKey.SetValue("HotTrackingColor", rgbDecimal, RegistryValueKind.String);
                        cpKey.Flush();
                    }
                }

                // ── 5. Dual broadcast ──────────────────────────────────────────────
                // "ImmersiveColorSet" refreshes the DWM/accent colour.
                // "WindowsThemeElement" triggers a secondary shell repaint pass
                // that Win11 23H2+ needs to update the taskbar live.
                BroadcastSettingsChange("ImmersiveColorSet");
                BroadcastSettingsChange("WindowsThemeElement");

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
                    SPI_SETDESKWALLPAPER, 0, imagePath,
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
            string build = Environment.OSVersion.Version.Build.ToString();
            return new SystemThemeInfo(isLight, accent, build);
        });
    }

    public Task<bool> ResetAccentColorAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var dwmKey = Registry.CurrentUser.OpenSubKey(DwmKey, writable: true);
                if (dwmKey is null) return false;

                dwmKey.DeleteValue("ColorizationColor",        throwOnMissingValue: false);
                dwmKey.DeleteValue("ColorizationColorBalance", throwOnMissingValue: false);
                dwmKey.DeleteValue("AccentColor",              throwOnMissingValue: false);
                dwmKey.DeleteValue("AccentColorInactive",      throwOnMissingValue: false);
                dwmKey.DeleteValue("ColorPrevalence",          throwOnMissingValue: false);
                dwmKey.Flush();

                using var pKey = Registry.CurrentUser.OpenSubKey(ThemesKey, writable: true);
                if (pKey != null)
                {
                    pKey.SetValue("ColorPrevalence", 0, RegistryValueKind.DWord);
                    pKey.Flush();
                }

                BroadcastSettingsChange("ImmersiveColorSet");
                BroadcastSettingsChange("WindowsThemeElement");
                return true;
            }
            catch { return false; }
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────

    private static bool IsLightModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemesKey);
            return key?.GetValue("AppsUseLightTheme") is 1;
        }
        catch { return true; }
    }

    private static void BroadcastSettingsChange(string param)
    {
        SendMessageTimeout(
            new IntPtr(-1),      // HWND_BROADCAST
            0x001A,              // WM_SETTINGCHANGE
            IntPtr.Zero,
            param,
            0x0002,              // SMTO_ABORTIFHUNG
            5000,
            out _);
    }

    /// <summary>
    /// Builds the 32-byte AccentPalette binary blob (8 shades × 4 bytes BGRA)
    /// that Windows 11 reads from HKCU\...\Explorer\Accent.
    /// Shades progress from darkest (index 0) to lightest (index 7).
    /// </summary>
    private static byte[] BuildAccentPalette(byte r, byte g, byte b)
    {
        float[] factors = { 0.35f, 0.50f, 0.65f, 0.80f, 1.00f, 1.15f, 1.35f, 1.60f };
        var palette = new byte[32];

        for (int i = 0; i < 8; i++)
        {
            byte sr = (byte)Math.Clamp((int)(r * factors[i]), 0, 255);
            byte sg = (byte)Math.Clamp((int)(g * factors[i]), 0, 255);
            byte sb = (byte)Math.Clamp((int)(b * factors[i]), 0, 255);

            // BGRA (little-endian, Windows convention)
            palette[i * 4 + 0] = sb;
            palette[i * 4 + 1] = sg;
            palette[i * 4 + 2] = sr;
            palette[i * 4 + 3] = 0xFF;
        }

        return palette;
    }

    private static uint HexToArgb(string hex)
    {
        hex = hex.TrimStart('#').Trim();
        hex = hex.Length switch
        {
            3 => string.Concat("FF", hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]),
            6 => "FF" + hex,
            8 => hex,
            _ => "FF" + hex.PadLeft(6, '0')
        };
        return Convert.ToUInt32(hex, 16);
    }

    private static string ArgbToHex(uint argb) => $"#{(argb & 0x00FFFFFF):X6}";
}
