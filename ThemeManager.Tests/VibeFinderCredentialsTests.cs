using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;

namespace ThemeManager.Tests;

public sealed class VibeFinderCredentialsTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", "password", false)]
    [InlineData("user", "  ", false)]
    [InlineData("listener", "hunter2", false)]
    [InlineData(" LISTENER ", "hunter2", false)]
    [InlineData("listener", "another-password", true)]
    [InlineData("another-user", "hunter2", true)]
    public void OnlyEmptyOrBundledExampleCredentialsAreUnconfigured(string? user, string? password, bool expected) =>
        Assert.Equal(expected, VibeFinderCredentials.IsConfigured(user, password));

    [Theory]
    [InlineData("||$theme")]
    [InlineData("listener|hunter2|$theme")]
    public void UnconfiguredWidgetShowsSignInPromptWithoutStartingFetch(string target)
    {
        using var measure = new VibeFinderMeasure("test", MeasureType.VibeTrackTitle, target);
        measure.Refresh();
        Assert.Equal("Sign in to VibeFinder", measure.Text);
        Assert.Null(measure.ActionUrl);
    }
}
