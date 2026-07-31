using System.Runtime.InteropServices;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>Physical RAM usage percentage, read via kernel32's GlobalMemoryStatusEx.</summary>
public sealed class MemoryMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "0%";

    private readonly ILogger _logger;

    public MemoryMeasure(string name, ILogger? logger = null)
    {
        Name = name;
        _logger = logger ?? NullLogger.Instance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public void Refresh()
    {
        try
        {
            var status = new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(),
            };

            if (GlobalMemoryStatusEx(ref status))
            {
                // dwMemoryLoad is already an approximate 0-100 percentage — no math needed.
                Value = status.dwMemoryLoad;
                Text = $"{Value:F0}%";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MemoryMeasure refresh failed; keeping last known value");
        }
    }
}
