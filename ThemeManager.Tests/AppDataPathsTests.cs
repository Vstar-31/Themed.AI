using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class AppDataPathsTests
{
    [Fact]
    public void NormalLaunchUsesWindowsLocalDataFolder() =>
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDataPaths.ResolveLocalRoot(null));

    [Theory]
    [InlineData("relative-folder")]
    [InlineData("C:relative-folder")]
    public void RelativeOverridesCannotAccidentallyUseTheWorkingDirectory(string path) =>
        Assert.Throws<ArgumentException>(() => AppDataPaths.ResolveLocalRoot(path));

    [Fact]
    public void AbsoluteOverrideIsNormalized() =>
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ThemedAI-QA")),
            AppDataPaths.ResolveLocalRoot(Path.Combine(Path.GetTempPath(), "ThemedAI-QA")));
}
