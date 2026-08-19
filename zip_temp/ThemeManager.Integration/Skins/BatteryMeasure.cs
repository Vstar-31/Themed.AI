using System.Runtime.InteropServices;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Battery charge percentage, via kernel32's GetSystemPowerStatus — the same API Windows'
/// own battery flyout is built on. On a desktop with no battery, Windows reports 255
/// ("unknown") for BatteryLifePercent, so Text falls back to "No battery" instead of a
/// meaningless number.
/// </summary>
public sealed class BatteryMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "—";

    private readonly ILogger _logger;

    public BatteryMeasure(string name, ILogger? logger = null)
    {
        Name = name;
        _logger = logger ?? NullLogger.Instance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;      // 0 = on battery, 1 = plugged in (AC), 255 = unknown
        public byte BatteryFlag;
        public byte BatteryLifePercent; // 0-100, or 255 = unknown (typically: no battery present)
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public void Refresh()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status)) return;

            if (status.BatteryLifePercent == 255)
            {
                Value = 0;
                Text = "No battery";
                return;
            }

            Value = status.BatteryLifePercent;
            bool charging = status.ACLineStatus == 1;
            Text = charging ? $"{Value:F0}% (charging)" : $"{Value:F0}%";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BatteryMeasure refresh failed; keeping last known value");
        }
    }
}
