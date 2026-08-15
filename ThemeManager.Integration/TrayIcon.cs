using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration;

/// <summary>
/// A Windows system tray icon, built from scratch with plain user32/shell32 P/Invoke —
/// WinUI3 has no NotifyIcon equivalent, and pulling in a NuGet package (e.g. H.NotifyIcon)
/// isn't an option here, so this recreates the small piece of it this app actually needs.
///
/// Mechanically: creates one small, invisible, message-only native window purely to receive
/// the tray icon's callback messages (left-click, right-click), completely separate from any
/// WinUI3/XAML window. Since it's created on the same thread that's already pumping WinUI3's
/// own message loop, no extra message pump is needed — messages just arrive on the normal
/// dispatcher tick, and every event this class raises fires on that same UI thread.
///
/// EXPERIMENTAL NOTE: the native window-procedure callback (<see cref="WndProc"/>) is the one
/// genuinely fragile part — if its delegate were ever garbage collected while Windows still
/// held a pointer to it, the process would crash. It's kept alive for the class's whole
/// lifetime via <see cref="_wndProcDelegate"/>, an instance field (never a local/lambda passed
/// directly to native code) specifically to prevent that. This was written and reviewed
/// carefully but, like everything native in this project, verified against documentation and
/// community examples rather than a live Windows run — see the integration notes for how to
/// tell quickly if something's wrong.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_TRAYCALLBACK = 0x8001; // WM_APP + 1 — arbitrary but must stay >= WM_APP (0x8000)
    private const uint WM_HOTKEY = 0x0312;

    private const uint MF_STRING = 0x00000000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_NONOTIFY = 0x0080;
    private const int IDI_APPLICATION = 32512; // stock system icon — zero-dependency, always present

    private const int CMD_OPEN = 1;
    private const int CMD_EXIT = 2;

    private const int HOTKEY_ID = 9000;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint VK_W = 0x57;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("shell32.dll")]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly ILogger _logger;
    private readonly WndProcDelegate _wndProcDelegate; // held for life — see class remarks
    private IntPtr _hwnd;
    private bool _iconAdded;

    /// <summary>Left-click, or "Open" from the right-click menu.</summary>
    public event Action? OpenRequested;

    /// <summary>"Exit" from the right-click menu.</summary>
    public event Action? ExitRequested;

    /// <summary>Global hotkey pressed (Win+Shift+W).</summary>
    public event Action? GlobalHotkeyActivated;

    public TrayIcon(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _wndProcDelegate = WndProc; // capture once, into a field, before any native code can see it
    }

    /// <summary>Creates the hidden window and shows the tray icon. Safe to call once; failures are logged, never thrown.</summary>
    public void Show()
    {
        try
        {
            const string className = "ThemedAI_TrayIconWindow";
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                lpszClassName = className,
            };
            RegisterClassEx(ref wc); // if this class is already registered (e.g. hot-reload during dev), the CreateWindowEx below still succeeds

            // HWND_MESSAGE (-3) makes this a message-only window: no taskbar entry, nothing to
            // paint, ever — it exists purely to receive the tray icon's callback messages.
            _hwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                _logger.LogWarning("Tray icon window creation failed; continuing without a tray icon");
                return;
            }

            // Register global hotkey (Win+Shift+W)
            if (!RegisterHotKey(_hwnd, HOTKEY_ID, MOD_WIN | MOD_SHIFT, VK_W))
                _logger.LogWarning("Failed to register global hotkey (Win+Shift+W); another app may be using it");

            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYCALLBACK,
                hIcon = LoadIcon(IntPtr.Zero, new IntPtr(IDI_APPLICATION)),
                szTip = "Themed.AI",
            };

            _iconAdded = Shell_NotifyIcon(NIM_ADD, ref data);
            if (!_iconAdded)
                _logger.LogWarning("Shell_NotifyIcon(NIM_ADD) failed; continuing without a tray icon");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create tray icon; the app will keep working, just without one");
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WM_TRAYCALLBACK)
            {
                uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF); // LOWORD — classic (pre-v4) tray callback layout
                if (mouseMsg == WM_LBUTTONUP) OpenRequested?.Invoke();
                else if (mouseMsg == WM_RBUTTONUP) ShowContextMenu();
                return IntPtr.Zero;
            }
            else if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                GlobalHotkeyActivated?.Invoke();
                return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            // A WndProc must never let an exception cross back into native code — that
            // corrupts the message loop. Log and swallow instead.
            _logger.LogWarning(ex, "Tray icon message handling failed");
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        IntPtr menu = IntPtr.Zero;
        try
        {
            GetCursorPos(out var pt);
            menu = CreatePopupMenu();
            AppendMenu(menu, MF_STRING, CMD_OPEN, "Open Themed.AI");
            AppendMenu(menu, MF_STRING, CMD_EXIT, "Exit");

            // Required so the menu dismisses correctly if the user clicks away from it —
            // a message-only window has no natural foreground behavior of its own.
            SetForegroundWindow(_hwnd);
            int cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_NONOTIFY, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

            if (cmd == CMD_OPEN) OpenRequested?.Invoke();
            else if (cmd == CMD_EXIT) ExitRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tray icon context menu failed");
        }
        finally
        {
            if (menu != IntPtr.Zero) DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_hwnd != IntPtr.Zero)
                UnregisterHotKey(_hwnd, HOTKEY_ID);

            if (_iconAdded)
            {
                var data = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hwnd, uID = 1 };
                Shell_NotifyIcon(NIM_DELETE, ref data);
            }
            if (_hwnd != IntPtr.Zero)
                DestroyWindow(_hwnd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tray icon cleanup failed (harmless if the process is exiting anyway)");
        }
    }
}
