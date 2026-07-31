using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Plain user32.dll window-style tricks for turning a normal window into a Rainmeter-style
/// floating widget: optional click-through, and hiding it from the taskbar/Alt+Tab.
/// Deliberately has no dependency on WinUI/Windows App SDK types — like the rest of this
/// project, it only needs a raw HWND. True per-pixel transparency needs WinUI's Composition
/// APIs instead, so that piece lives in SkinHostWindow.xaml.cs in the WinUI project, where a
/// Window/Compositor is already naturally available.
///
/// Every method here is failure-tolerant by design: a widget should never crash the app just
/// because one of these OS-level tricks didn't take on a particular Windows build.
/// </summary>
public static class SkinWindowInterop
{
    private const int GWL_EXSTYLE = -20;

    private const long WS_EX_TRANSPARENT = 0x00000020; // mouse clicks pass through to whatever's behind
    private const long WS_EX_TOOLWINDOW  = 0x00000080; // hide from taskbar
    private const long WS_EX_APPWINDOW   = 0x00040000; // (cleared) forces a taskbar entry when set
    private const long WS_EX_NOACTIVATE  = 0x08000000; // don't steal foreground focus when shown

    // Windows 11 is x64-only, but WinUI3/unpackaged apps can in principle run x86 on Windows 10;
    // GetWindowLongPtr/SetWindowLongPtr don't exist as real exports on 32-bit Windows (they're
    // macros there), so this picks the correct pair at runtime rather than assuming 64-bit.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetExStyle(IntPtr hWnd) =>
        Environment.Is64BitProcess ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE) : GetWindowLongPtr32(hWnd, GWL_EXSTYLE);

    private static void SetExStyle(IntPtr hWnd, IntPtr value)
    {
        if (Environment.Is64BitProcess) SetWindowLongPtr64(hWnd, GWL_EXSTYLE, value);
        else SetWindowLongPtr32(hWnd, GWL_EXSTYLE, value);
    }

    /// <summary>Turns click-through on or off for this window. Safe to call repeatedly.</summary>
    public static void SetClickThrough(IntPtr hwnd, bool enabled, ILogger? logger = null)
    {
        try
        {
            long current = GetExStyle(hwnd).ToInt64();
            long updated = enabled ? current | WS_EX_TRANSPARENT : current & ~WS_EX_TRANSPARENT;
            SetExStyle(hwnd, new IntPtr(updated));
        }
        catch (Exception ex)
        {
            (logger ?? NullLogger.Instance).LogWarning(ex, "Failed to set click-through state on skin window");
        }
    }

    /// <summary>
    /// Hides the window from the taskbar and Alt+Tab switcher, and stops it from stealing
    /// foreground focus when it's created or moved — matching how real Rainmeter skins behave.
    /// Call once, right after the window is created.
    /// </summary>
    public static void HideFromTaskbarAndAltTab(IntPtr hwnd, ILogger? logger = null)
    {
        try
        {
            long current = GetExStyle(hwnd).ToInt64();
            long updated = (current | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
            SetExStyle(hwnd, new IntPtr(updated));
        }
        catch (Exception ex)
        {
            (logger ?? NullLogger.Instance).LogWarning(ex, "Failed to hide skin window from taskbar/Alt+Tab");
        }
    }
}
