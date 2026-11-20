using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class WorldAutoSwitchSelectorTests
{
    [Theory]
    [InlineData("Euphoric", "neon")]
    [InlineData("Heartbreak", "nocturnal")]
    [InlineData("Focus", "minimal")]
    public void FindsEligibleWorldForRecognizedMood(string mood, string targetTag)
    {
        var active = Scene("active", "cozy", autoSwitch: true);
        var candidate = Scene("target", targetTag);

        var selected = WorldAutoSwitchSelector.FindCandidate(mood, null, active, [active, candidate]);

        Assert.Same(candidate, selected);
    }

    [Fact]
    public void LearnsMoodOnlyAfterEnoughSignals()
    {
        var active = Scene("active", "cozy", autoSwitch: true);
        var nocturnal = Scene("night", "nocturnal");
        var profile = Profile(signals: 3, moods: new Dictionary<string, double> { ["Nocturnal"] = 1 });

        var selected = WorldAutoSwitchSelector.FindCandidate("Bright", profile, active, [active, nocturnal]);

        Assert.Same(nocturnal, selected);
    }

    [Fact]
    public void SkipsExplicitAndNonReactiveCandidates()
    {
        var active = Scene("active", "cozy", autoSwitch: true);
        var explicitWorld = Scene("explicit", "neon", requiresExplicit: true);
        var inactiveWorld = Scene("inactive", "neon", reactive: false);
        var eligible = Scene("eligible", "neon");

        var selected = WorldAutoSwitchSelector.FindCandidate("Intense", null, active,
            [active, explicitWorld, inactiveWorld, eligible]);

        Assert.Same(eligible, selected);
    }

    [Fact]
    public void UsesLearnedThemeAndAmbienceWhenMoodHasNoMapping()
    {
        var active = Scene("active", "cozy", autoSwitch: true);
        var neon = Scene("neon", "neon");
        var themeProfile = Profile(signals: 1, themes: new Dictionary<string, double> { ["Dark neon"] = 2 });
        Assert.Same(neon, WorldAutoSwitchSelector.FindCandidate("Neutral", themeProfile, active, [active, neon]));

        var night = Scene("night", "nocturnal");
        var ambienceProfile = Profile(signals: 1, ambience: new Dictionary<string, double> { ["Night drive"] = 2 });
        Assert.Same(night, WorldAutoSwitchSelector.FindCandidate("Neutral", ambienceProfile, active, [active, night]));
    }

    [Fact]
    public void DoesNotSwitchWhenActiveWorldOptedOutOrAlreadyMatches()
    {
        var optedOut = Scene("active", "cozy");
        var neon = Scene("neon", "neon");
        Assert.Null(WorldAutoSwitchSelector.FindCandidate("Intense", null, optedOut, [optedOut, neon]));

        var matching = Scene("active", "neon", autoSwitch: true);
        Assert.Null(WorldAutoSwitchSelector.FindCandidate("Intense", null, matching, [matching, neon]));
    }

    private static DesktopScene Scene(string id, string tag, bool autoSwitch = false, bool reactive = true, bool requiresExplicit = false) => new()
    {
        Id = id,
        Tags = [tag],
        RequiresExplicitActivation = requiresExplicit,
        Behavior = new SceneBehavior { AutoSwitch = autoSwitch, ReactToVibeFinder = reactive }
    };

    private static VibePersonalizationProfile Profile(
        double signals,
        IReadOnlyDictionary<string, double>? moods = null,
        IReadOnlyDictionary<string, double>? themes = null,
        IReadOnlyDictionary<string, double>? ambience = null) => new(
            signals,
            moods ?? new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            themes ?? new Dictionary<string, double>(),
            ambience ?? new Dictionary<string, double>(),
            Array.Empty<string>());
}
