using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Total (all-core) CPU usage, read via the same Win32 API Task Manager itself is built on.
/// No admin rights, no external package — just kernel32.
/// </summary>
public sealed class CpuMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "0%";

    private readonly ILogger _logger;
    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevious;

    public CpuMeasure(string name, ILogger? logger = null)
    {
        Name = name;
        _logger = logger ?? NullLogger.Instance;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    private static ulong ToUInt64(FILETIME ft) =>
        ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    public void Refresh()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                return; // keep the last known value rather than snapping to 0

            ulong idleNow = ToUInt64(idle);
            ulong kernelNow = ToUInt64(kernel);
            ulong userNow = ToUInt64(user);

            if (_hasPrevious)
            {
                ulong idleDelta = idleNow - _prevIdle;
                // Kernel time reported by Windows already includes idle time, so
                // total = kernelDelta + userDelta (not + idleDelta again).
                ulong kernelDelta = kernelNow - _prevKernel;
                ulong userDelta = userNow - _prevUser;
                ulong totalDelta = kernelDelta + userDelta;

                if (totalDelta > 0)
                {
                    Value = Math.Clamp(100.0 * (1.0 - (double)idleDelta / totalDelta), 0, 100);
                    Text = $"{Value:F0}%";
                }
            }

            _prevIdle = idleNow;
            _prevKernel = kernelNow;
            _prevUser = userNow;
            _hasPrevious = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CpuMeasure refresh failed; keeping last known value");
        }
    }
}
