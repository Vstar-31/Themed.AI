using ThemeManager.Integration.Skins;

namespace ThemeManager.Tests;

public sealed class DiskMeasureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NumericAndTextValuesUseTheRequestedWindowsDriveMetric(bool free)
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;
        var drive = new DriveInfo(root);
        var measure = new DiskMeasure("test", root, free);
        measure.Refresh();
        var freePercent = 100.0 * drive.AvailableFreeSpace / drive.TotalSize;
        var expected = free ? freePercent : 100.0 - freePercent;
        Assert.InRange(measure.Value, expected - 0.1, expected + 0.1);
        Assert.Equal($"{measure.Value:F0}%", measure.Text);
    }
}
