using System;
using System.Runtime.InteropServices;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Per-core CPU usage via NtQuerySystemInformation(SystemProcessorPerformanceInformation) — an
/// undocumented-by-Microsoft but extremely well-established NTAPI; it's what Task Manager's own
/// per-core view is built on, and has been the standard technique for this in the Windows
/// systems-programming community for decades. CpuMeasure's GetSystemTimes has no per-core
/// variant — this is a genuinely different API, not a small tweak to CpuMeasure.
///
/// Target holds the zero-based core index as a string (e.g. "0", "1", ...), reusing the same
/// Target-field pattern WeatherMeasure/WebJsonMeasure already use rather than inventing new
/// editor UI. Blank/invalid/out-of-range Target falls back to core 0 — same "keep it usable"
/// instinct as DiskMeasure falling back to C:\ rather than failing outright.
/// </summary>
public sealed class CpuCoreMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "0%";

    private readonly int _coreIndex;
    private readonly ILogger _logger;
    private long _prevIdle, _prevTotal;
    private bool _hasPrevious;

    // Native layout is 5x LARGE_INTEGER (8 bytes each) + 1x ULONG (4 bytes) = 44 bytes, padded
    // to 48 for the struct's 8-byte alignment (its largest member is 8 bytes). Size is forced
    // explicitly rather than left to automatic computation — this struct is only ever used as
    // an array element, so getting the stride exactly right matters, and this can't be verified
    // by running it here.
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    private struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public int InterruptCount;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, out int ReturnLength);

    private const int SystemProcessorPerformanceInformation = 8;

    public CpuCoreMeasure(string name, string? target, ILogger? logger = null)
    {
        Name = name;
        _coreIndex = int.TryParse(target, out int idx) && idx >= 0 ? idx : 0;
        _logger = logger ?? NullLogger.Instance;
    }

    public void Refresh()
    {
        int coreCount = Environment.ProcessorCount;
        if (_coreIndex >= coreCount)
        {
            Text = "No Core";
            return;
        }

        int structSize = Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>();
        IntPtr buffer = Marshal.AllocHGlobal(structSize * coreCount);
        try
        {
            int status = NtQuerySystemInformation(SystemProcessorPerformanceInformation, buffer, structSize * coreCount, out _);
            if (status != 0) // non-zero NTSTATUS = failure; keep last known value
                return;

            var info = Marshal.PtrToStructure<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(buffer + (structSize * _coreIndex));

            long idleNow = info.IdleTime;
            long totalNow = info.IdleTime + info.KernelTime + info.UserTime;

            if (_hasPrevious)
            {
                long idleDelta = idleNow - _prevIdle;
                long totalDelta = totalNow - _prevTotal;

                if (totalDelta > 0)
                {
                    Value = Math.Clamp(100.0 * (1.0 - (double)idleDelta / totalDelta), 0, 100);
                    Text = $"{Value:F0}%";
                }
            }

            _prevIdle = idleNow;
            _prevTotal = totalNow;
            _hasPrevious = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CpuCoreMeasure refresh failed for core {Core}; keeping last known value", _coreIndex);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
