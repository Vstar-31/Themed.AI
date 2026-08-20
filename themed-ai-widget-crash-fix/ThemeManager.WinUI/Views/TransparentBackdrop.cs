using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// A custom backdrop that forces a WinUI 3 window to have a fully transparent background.
/// WinUI 3 has no native transparent backdrop, so this uses the classic DwmEnableBlurBehindWindow trick.
/// </summary>
public class TransparentBackdrop : SystemBackdrop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public bool fTransitionOnMaximized;
    }

    private const uint DWM_BB_ENABLE = 0x00000001;
    private const uint DWM_BB_BLURREGION = 0x00000002;

    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private ICompositionSupportsSystemBackdrop? _connectedTarget;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        _connectedTarget = connectedTarget;

        if (connectedTarget is Window window)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            EnableTransparentBackground(hwnd);
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop connectedTarget)
    {
        base.OnTargetDisconnected(connectedTarget);
        if (_connectedTarget == connectedTarget)
        {
            _connectedTarget = null;
        }
    }

    private static void EnableTransparentBackground(IntPtr hwnd)
    {
        // By creating an empty region and enabling blur, DWM removes the solid background color.
        IntPtr hRgn = CreateRectRgn(0, 0, -1, -1);
        try
        {
            var bb = new DWM_BLURBEHIND
            {
                dwFlags = DWM_BB_ENABLE | DWM_BB_BLURREGION,
                fEnable = true,
                hRgnBlur = hRgn
            };
            DwmEnableBlurBehindWindow(hwnd, ref bb);
        }
        catch
        {
            // Fail gracefully if DWM APIs fail on this build of Windows
        }
        finally
        {
            DeleteObject(hRgn);
        }
    }
}
