using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Free or used space percentage for a drive, via the built-in <see cref="DriveInfo"/> class.
/// No P/Invoke needed here — DriveInfo already wraps the right Win32 calls for us.
/// </summary>
public sealed class DiskMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "0%";

    private readonly string _driveTarget;
    private readonly bool _reportFreeSpace;
    private readonly ILogger _logger;

    /// <param name="driveTarget">Drive root, e.g. "C:\". Falls back to "C:\" if null/empty.</param>
    /// <param name="reportFreeSpace">True = Value/Text describe free space; false = used space.</param>
    public DiskMeasure(string name, string? driveTarget, bool reportFreeSpace, ILogger? logger = null)
    {
        Name = name;
        _driveTarget = string.IsNullOrWhiteSpace(driveTarget) ? @"C:\" : driveTarget;
        _reportFreeSpace = reportFreeSpace;
        _logger = logger ?? NullLogger.Instance;
    }

    public void Refresh()
    {
        try
        {
            var drive = new DriveInfo(_driveTarget);
            if (!drive.IsReady)
                return; // e.g. an empty optical/removable drive letter — keep last known value

            double total = drive.TotalSize;
            if (total <= 0)
                return;

            double freePercent = 100.0 * drive.AvailableFreeSpace / total;
            double usedPercent = 100.0 - freePercent;
            
            // Value always represents used space so that progress bars fill up as the disk gets full
            // and threshold alerts (e.g. >90%) trigger correctly when the disk is almost full.
            Value = usedPercent;
            
            // Text contains the string for the requested metric
            Text = _reportFreeSpace ? $"{freePercent:F0}%" : $"{usedPercent:F0}%";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DiskMeasure refresh failed for {Drive}; keeping last known value", _driveTarget);
        }
    }
}
