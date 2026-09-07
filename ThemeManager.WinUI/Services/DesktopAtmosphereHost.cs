using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Owns one transparent atmosphere window per physical monitor. The windows are attached to
/// Explorer's desktop WorkerW, so the visual world lives behind desktop icons instead of over
/// normal applications.
/// </summary>
public sealed class DesktopAtmosphereHost : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<DesktopAtmosphereWindow> _windows = new();
    private bool _started;
    private bool _disposed;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc callback, IntPtr dwData);

    public DesktopAtmosphereHost(ILogger logger) => _logger = logger;

    public async Task StartAsync(DesktopScene? scene, DesktopVibeAdapter.Atmosphere atmosphere)
    {
        if (_disposed || _started) return;
        _started = true;

        var monitors = new List<IntPtr>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            monitors.Add(monitor);
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            _logger.LogWarning("Desktop atmosphere found no display monitors");
            return;
        }

        for (var i = 0; i < monitors.Count; i++)
        {
            try
            {
                var window = new DesktopAtmosphereWindow(monitors[i], i, _logger);
                window.ApplyScene(scene, atmosphere);
                _windows.Add(window);
                await window.AttachAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create atmosphere layer for monitor {Monitor}", i);
            }
        }
    }

    public void ApplyScene(DesktopScene? scene, DesktopVibeAdapter.Atmosphere atmosphere)
    {
        foreach (var window in _windows)
            window.ApplyScene(scene, atmosphere);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var window in _windows)
            window.Dispose();
        _windows.Clear();
    }
}
