using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Attaches a window behind the desktop icons (in front of the wallpaper) — the same
/// undocumented technique Rainmeter and most "live wallpaper" tools use: ask Explorer's
/// Program Manager to spawn a helper window (WorkerW), find the specific instance of it that
/// sits behind the icons, then reparent our window into it.
///
/// EXPERIMENTAL — more so than anything else in this project. This relies on an internal,
/// completely undocumented Explorer message (0x052C) that Microsoft has never published and
/// could change or remove at any time. Independent reports from Windows 11 24H2 describe both
/// the WorkerW sometimes not appearing until a moment after the message is sent, and — on some
/// builds — not appearing at all. <see cref="TryAttach"/> retries a few times to absorb the
/// first kind of flakiness, and always returns a clean false (never throws, never hangs) for
/// the second — the caller falls back to plain always-on-top mode, which is what every widget
/// already uses by default.
/// </summary>
public static class DesktopLayerInterop
{
    private const uint SMTO_NORMAL = 0x0000;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Tries to reparent <paramref name="hwnd"/> behind the desktop icons. Returns true only if
    /// it actually succeeded — the caller should keep the widget in normal always-on-top mode
    /// on false, not assume anything was left in a half-attached state (SetParent is only ever
    /// called on success).
    /// </summary>
    public static bool TryAttach(IntPtr hwnd, ILogger? logger = null)
    {
        var log = logger ?? NullLogger.Instance;
        try
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
            {
                log.LogWarning("Desktop-layer attach: couldn't find Progman; staying always-on-top");
                return false;
            }

            // Up to 3 tries: reports from Windows 11 24H2 describe the WorkerW sometimes not
            // existing immediately after this message, only "a moment later".
            for (int attempt = 0; attempt < 3; attempt++)
            {
                SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out _);

                IntPtr targetWorkerW = FindWorkerWBehindIcons();
                if (targetWorkerW != IntPtr.Zero)
                {
                    SetParent(hwnd, targetWorkerW);
                    return true;
                }

                Thread.Sleep(300);
            }

            log.LogWarning("Desktop-layer attach: no WorkerW appeared after 3 tries " +
                            "(known to happen on some Windows 11 builds) — staying always-on-top");
            return false;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Desktop-layer attach failed; staying always-on-top");
            return false;
        }
    }

    /// <summary>Undoes <see cref="TryAttach"/> — reparents back to the desktop as a normal top-level window.</summary>
    public static void Detach(IntPtr hwnd, ILogger? logger = null)
    {
        try
        {
            SetParent(hwnd, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            (logger ?? NullLogger.Instance).LogWarning(ex, "Desktop-layer detach failed");
        }
    }

    /// <summary>
    /// Windows creates (at least) two WorkerW instances: one hosts SHELLDLL_DefView (the actual
    /// desktop icons), the other sits behind it and is the one we want — this walks every
    /// WorkerW-classed window and picks the one WITHOUT that child.
    /// </summary>
    private static IntPtr FindWorkerWBehindIcons()
    {
        IntPtr target = IntPtr.Zero;

        EnumWindows((hwnd, _) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            if (sb.ToString() != "WorkerW")
                return true; // keep enumerating

            IntPtr shellView = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                target = hwnd; // this WorkerW does NOT host the icons — it's the one behind them
                return false;  // stop enumerating
            }

            return true;
        }, IntPtr.Zero);

        return target;
    }
}
