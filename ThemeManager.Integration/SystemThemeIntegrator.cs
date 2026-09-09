using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using ThemeManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration;

public sealed class SystemThemeIntegrator : ISystemThemeIntegrator
{
    private readonly ILogger _logger;

    public SystemThemeIntegrator(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfoSetWallpaper(uint uiAction, uint uiParam, string? pvParam, uint fWinIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfoGetWallpaper(uint uiAction, uint uiParam, [Out] StringBuilder pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPI_GETDESKWALLPAPER = 0x0073;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    private const string DwmKey = @"SOFTWARE\Microsoft\Windows\DWM";
    private const string ThemesKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ExplorerKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string ControlColors = @"Control Panel\Colors";
    private const string DesktopKey = @"Control Panel\Desktop";

    public Task<string> GetCurrentAccentColorAsync() => Task.Run(() =>
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("ColorizationColor") is int raw)
                return ArgbToHex((uint)raw);
        }
        catch { }
        return "#7D5A44";
    });

    public Task<bool> ApplyAccentColorAsync(string hexColor) => Task.Run(() =>
    {
        try
        {
            _logger.LogInformation("Applying accent color {HexColor} to DWM/Registry", hexColor);
            uint argb = HexToArgb(hexColor);
            byte r = (byte)((argb >> 16) & 0xFF), g = (byte)((argb >> 8) & 0xFF), b = (byte)(argb & 0xFF);
            uint cArgb = 0xC4000000u | ((uint)r << 16) | ((uint)g << 8) | b;
            uint abgr = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;

            using (var dwmKey = Registry.CurrentUser.OpenSubKey(DwmKey, true))
            {
                if (dwmKey is null) return false;
                dwmKey.SetValue("ColorizationColor", (int)cArgb, RegistryValueKind.DWord);
                dwmKey.SetValue("ColorizationColorBalance", 100, RegistryValueKind.DWord);
                dwmKey.SetValue("AccentColor", (int)abgr, RegistryValueKind.DWord);
                dwmKey.SetValue("AccentColorInactive", (int)abgr, RegistryValueKind.DWord);
                dwmKey.SetValue("ColorPrevalence", 1, RegistryValueKind.DWord);
                dwmKey.SetValue("AutoColorization", 0, RegistryValueKind.DWord);
                dwmKey.Flush();
            }

            using (var pKey = Registry.CurrentUser.OpenSubKey(ThemesKey, true))
            {
                if (pKey != null)
                {
                    pKey.SetValue("ColorPrevalence", 1, RegistryValueKind.DWord);
                    pKey.SetValue("AutoColorization", 0, RegistryValueKind.DWord);
                    pKey.Flush();
                }
            }

            using (var accentKey = Registry.CurrentUser.CreateSubKey(ExplorerKey))
            {
                if (accentKey != null)
                {
                    accentKey.SetValue("AccentPalette", BuildAccentPalette(r, g, b), RegistryValueKind.Binary);
                    accentKey.SetValue("StartColorMenu", (int)abgr, RegistryValueKind.DWord);
                    accentKey.SetValue("AccentColorMenu", (int)abgr, RegistryValueKind.DWord);
                    accentKey.Flush();
                }
            }

            string rgb = $"{r} {g} {b}";
            using (var cpKey = Registry.CurrentUser.OpenSubKey(ControlColors, true))
            {
                if (cpKey != null)
                {
                    cpKey.SetValue("Hilight", rgb, RegistryValueKind.String);
                    cpKey.SetValue("HotTrackingColor", rgb, RegistryValueKind.String);
                    cpKey.Flush();
                }
            }

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

    public Task<bool> ApplyWallpaperAsync(string imagePath) => Task.Run(() =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                _logger.LogWarning("Wallpaper apply skipped: empty path");
                return false;
            }

            var fullPath = Path.GetFullPath(imagePath);
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Wallpaper apply failed: file does not exist: {Path}", fullPath);
                return false;
            }

            var sourceInfo = new FileInfo(fullPath);
            if (sourceInfo.Length <= 0)
            {
                _logger.LogWarning("Wallpaper apply failed: file is empty: {Path}", fullPath);
                return false;
            }

            var stablePath = CopyWallpaperToWindowsCache(fullPath);
            _logger.LogInformation("Applying wallpaper {SourcePath} via stable path {StablePath} ({Bytes} bytes)", fullPath, stablePath, sourceInfo.Length);

            try
            {
                var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
                try
                {
                    desktopWallpaper.SetWallpaper(null, stablePath);
                    desktopWallpaper.SetPosition(DesktopWallpaperPosition.Fill);
                    _logger.LogInformation("Wallpaper applied through IDesktopWallpaper");
                }
                finally
                {
                    Marshal.ReleaseComObject(desktopWallpaper);
                }

                PersistWallpaperPresentationSettings();
                if (VerifyWallpaperPath(stablePath))
                    return true;

                _logger.LogWarning("IDesktopWallpaper reported success but verification did not match; trying SPI fallback");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IDesktopWallpaper failed; falling back to SystemParametersInfoW");
            }

            bool ok = SystemParametersInfoSetWallpaper(
                SPI_SETDESKWALLPAPER,
                0,
                stablePath,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            if (!ok)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogError("Windows rejected wallpaper {Path}. Win32 error {Error}: {Message}", stablePath, error, new Win32Exception(error).Message);
                return false;
            }

            PersistWallpaperPresentationSettings();
            var verified = VerifyWallpaperPath(stablePath);
            _logger.LogInformation("SystemParametersInfoW returned success. Verification={Verified}", verified);
            return verified;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply wallpaper {Path}", imagePath);
            return false;
        }
    });

    public Task<SystemThemeInfo> GetCurrentSystemThemeAsync() => Task.Run(() =>
    {
        bool isLight = IsLightModeEnabled();
        string accent = GetCurrentAccentColorAsync().GetAwaiter().GetResult();
        string build = Environment.OSVersion.Version.Build.ToString();
        return new SystemThemeInfo(isLight, accent, build);
    });

    public Task<bool> ResetAccentColorAsync() => Task.Run(() =>
    {
        try
        {
            using var dwmKey = Registry.CurrentUser.OpenSubKey(DwmKey, true);
            if (dwmKey is null) return false;
            foreach (var value in new[] { "ColorizationColor", "ColorizationColorBalance", "AccentColor", "AccentColorInactive", "ColorPrevalence", "AutoColorization" })
                dwmKey.DeleteValue(value, false);
            dwmKey.Flush();
            using var pKey = Registry.CurrentUser.OpenSubKey(ThemesKey, true);
            if (pKey != null)
            {
                pKey.SetValue("ColorPrevalence", 0, RegistryValueKind.DWord);
                pKey.SetValue("AutoColorization", 1, RegistryValueKind.DWord);
                pKey.Flush();
            }
            BroadcastSettingsChange("ImmersiveColorSet");
            BroadcastSettingsChange("WindowsThemeElement");
            return true;
        }
        catch { return false; }
    });

    public Task<bool> IsBatterySaverActiveAsync() => Task.Run(() =>
    {
        try { return Windows.System.Power.PowerManager.EnergySaverStatus == Windows.System.Power.EnergySaverStatus.On; }
        catch { return false; }
    });

    private static bool IsLightModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemesKey);
            return key?.GetValue("AppsUseLightTheme") is 1;
        }
        catch { return true; }
    }

    private static void BroadcastSettingsChange(string param) =>
        SendMessageTimeoutW(new IntPtr(-1), 0x001A, IntPtr.Zero, param, 0x0002, 5000, out _);

    private static string CopyWallpaperToWindowsCache(string sourcePath)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "ThemedAI");
        Directory.CreateDirectory(dir);

        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var safeName = string.Concat(sourceName.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "wallpaper";
        if (safeName.Length > 80) safeName = safeName[..80].Trim();

        var targetPath = Path.Combine(dir, $"{safeName}.bmp");
        File.Copy(sourcePath, targetPath, overwrite: true);
        return targetPath;
    }

    private static void PersistWallpaperPresentationSettings()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(DesktopKey);
            key?.SetValue("WallpaperStyle", "10", RegistryValueKind.String);
            key?.SetValue("TileWallpaper", "0", RegistryValueKind.String);
            key?.Flush();
        }
        catch { }
    }

    private bool VerifyWallpaperPath(string expectedPath)
    {
        try
        {
            var buffer = new StringBuilder(32768);
            if (!SystemParametersInfoGetWallpaper(SPI_GETDESKWALLPAPER, (uint)buffer.Capacity, buffer, 0))
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Could not verify current wallpaper. Win32 error {Error}: {Message}", error, new Win32Exception(error).Message);
                return false;
            }

            var actual = buffer.ToString();
            var verified = string.Equals(actual, expectedPath, StringComparison.OrdinalIgnoreCase);
            _logger.LogInformation("Current wallpaper path: {ActualPath}; expected: {ExpectedPath}; match={Match}", actual, expectedPath, verified);
            return verified;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wallpaper verification failed");
            return false;
        }
    }

    private static byte[] BuildAccentPalette(byte r, byte g, byte b)
    {
        float[] factors = { 0.35f, 0.50f, 0.65f, 0.80f, 1.00f, 1.15f, 1.35f, 1.60f };
        var palette = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            palette[i * 4] = (byte)Math.Clamp((int)(r * factors[i]), 0, 255);
            palette[i * 4 + 1] = (byte)Math.Clamp((int)(g * factors[i]), 0, 255);
            palette[i * 4 + 2] = (byte)Math.Clamp((int)(b * factors[i]), 0, 255);
        }
        return palette;
    }

    private static uint HexToArgb(string hex)
    {
        hex = hex.TrimStart('#').Trim();
        hex = hex.Length switch
        {
            3 => $"FF{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}",
            6 => "FF" + hex,
            8 => hex,
            _ => "FF" + hex.PadLeft(6, '0')
        };
        return Convert.ToUInt32(hex, 16);
    }

    private static string ArgbToHex(uint argb) => $"#{(argb & 0x00FFFFFF):X6}";

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out RECT rect);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(DesktopWallpaperPosition position);
        DesktopWallpaperPosition GetPosition();
        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void SetSlideshowOptions(DesktopSlideshowDirection options, uint slideshowTick);
        uint GetSlideshowOptions(out DesktopSlideshowDirection options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, DesktopSlideshowDirection direction);
        DesktopSlideshowState GetStatus();
        bool Enable();
    }

    [ComImport, Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass
    {
    }

    private enum DesktopWallpaperPosition
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    private enum DesktopSlideshowDirection
    {
        Forward = 0,
        Backward = 1
    }

    private enum DesktopSlideshowState
    {
        Enabled = 1,
        Slideshow = 2,
        DisabledByRemoteSession = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
