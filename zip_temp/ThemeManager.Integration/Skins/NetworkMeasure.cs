using System.Net.NetworkInformation;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Network throughput in KB/s (or MB/s once it's fast enough), computed from the delta
/// between two reads of total bytes sent/received across every active, non-loopback
/// interface. No P/Invoke needed — <see cref="NetworkInterface"/> is plain BCL.
/// </summary>
public sealed class NetworkMeasure : IMeasure
{
    public string Name { get; }

    /// <summary>Current throughput in KB/s.</summary>
    public double Value { get; private set; }

    public string Text { get; private set; } = "0 KB/s";

    private readonly bool _measureUpload;
    private readonly ILogger _logger;
    private long _prevBytes;
    private DateTime _prevTime;
    private bool _hasPrevious;

    public NetworkMeasure(string name, bool measureUpload, ILogger? logger = null)
    {
        Name = name;
        _measureUpload = measureUpload;
        _logger = logger ?? NullLogger.Instance;
    }

    public void Refresh()
    {
        try
        {
            long totalBytes = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var stats = nic.GetIPv4Statistics();
                totalBytes += _measureUpload ? stats.BytesSent : stats.BytesReceived;
            }

            var now = DateTime.UtcNow;
            if (_hasPrevious)
            {
                double elapsedSeconds = (now - _prevTime).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    long deltaBytes = totalBytes - _prevBytes;
                    // A negative delta can happen if an adapter resets its counters between
                    // ticks (sleep/wake, VPN reconnect) — clamp to 0 rather than show garbage.
                    Value = Math.Max(0, deltaBytes / elapsedSeconds / 1024.0);
                    Text = Value >= 1024 ? $"{Value / 1024.0:F1} MB/s" : $"{Value:F0} KB/s";
                }
            }

            _prevBytes = totalBytes;
            _prevTime = now;
            _hasPrevious = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NetworkMeasure refresh failed; keeping last known value");
        }
    }
}
