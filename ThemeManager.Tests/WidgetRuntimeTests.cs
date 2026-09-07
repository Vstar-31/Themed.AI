using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetRuntimeTests
{
    [Fact]
    public void Scheduler_OnlyReturnsWidgetsThatAreDue()
    {
        var scheduler = new WidgetTickScheduler();
        var fast = new SkinDefinition { Id = "fast", UpdateIntervalMs = 100 };
        var slow = new SkinDefinition { Id = "slow", UpdateIntervalMs = 1000 };

        Assert.Equal(new[] { "fast", "slow" }, scheduler.GetDue(new[] { fast, slow }, 0).Select(s => s.Id));
        Assert.Equal("fast", Assert.Single(scheduler.GetDue(new[] { fast, slow }, 100)).Id);
        Assert.Equal(new[] { "fast", "slow" }, scheduler.GetDue(new[] { fast, slow }, 1000).Select(s => s.Id));
    }

    [Fact]
    public void Scheduler_DoesNotCreateCatchUpBursts()
    {
        var scheduler = new WidgetTickScheduler();
        var widget = new SkinDefinition { Id = "widget", UpdateIntervalMs = 100 };

        Assert.Single(scheduler.GetDue(new[] { widget }, 0));
        Assert.Single(scheduler.GetDue(new[] { widget }, 100));
        // A long stall advances from the current observation rather than replaying every missed tick.
        Assert.Single(scheduler.GetDue(new[] { widget }, 5000));
        Assert.Empty(scheduler.GetDue(new[] { widget }, 5050));
    }

    [Fact]
    public void Scheduler_ClampsInvalidIntervalsToSafeBounds()
    {
        var scheduler = new WidgetTickScheduler();
        var tooFast = new SkinDefinition { Id = "fast", UpdateIntervalMs = 1 };

        Assert.Single(scheduler.GetDue(new[] { tooFast }, 0));
        Assert.Empty(scheduler.GetDue(new[] { tooFast }, 49));
        Assert.Single(scheduler.GetDue(new[] { tooFast }, 50));
    }

    [Fact]
    public void Validator_CatchesDuplicateMeasuresAndBrokenMeterReferences()
    {
        var skin = new SkinDefinition
        {
            Width = 0,
            Height = 100,
            UpdateIntervalMs = 20,
            Measures =
            {
                new MeasureDefinition { Name = "CPU" },
                new MeasureDefinition { Name = "cpu" },
            },
            Meters =
            {
                new MeterDefinition { MeasureName = "Missing", Width = 20, Height = 20 },
            },
        };

        var issues = WidgetDefinitionValidator.Validate(skin);

        Assert.Contains(issues, i => i.Path == "size");
        Assert.Contains(issues, i => i.Path == "updateIntervalMs");
        Assert.Contains(issues, i => i.Message.Contains("unique", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, i => i.Path.Contains("measureName", StringComparison.OrdinalIgnoreCase));
    }
}
