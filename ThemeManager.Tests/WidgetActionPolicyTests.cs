using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetActionPolicyTests
{
    [Theory]
    [InlineData("https://example.com/music")]
    [InlineData("http://example.com")]
    [InlineData("mailto:hello@example.com")]
    public void AllowsExternalLinks(string value) => Assert.True(WidgetActionPolicy.CanOpenExternal(value));

    [Theory]
    [InlineData(null)]
    [InlineData("calc.exe")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("\\\\server\\share\\run.exe")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.com\n")]
    [InlineData("plugin://installed/action")]
    public void NeverPassesFilesOrArbitraryHandlersToTheShell(string? value) => Assert.False(WidgetActionPolicy.CanOpenExternal(value));
}
