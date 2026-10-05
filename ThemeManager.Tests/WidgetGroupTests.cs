using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetGroupTests
{
    [Fact]
    public void Clone_CopiesGroupsAndMembershipIndependently()
    {
        var skin = new SkinDefinition
        {
            Id = "source",
            Groups =
            {
                new WidgetGroupDefinition
                {
                    Id = "group",
                    Name = "Media cluster",
                    X = 10,
                    Y = 20,
                    Width = 300,
                    Height = 150,
                    ScaleX = 1.2,
                    ScaleY = 0.8,
                    Rotation = 12,
                    Opacity = 0.7,
                    Clip = true,
                    MeterIds = { "meter-1" },
                    Variables = { ["accent"] = "#fff" }
                }
            }
        };

        var clone = skin.Clone("clone");

        Assert.Equal("clone", clone.Id);
        Assert.Single(clone.Groups);
        Assert.NotSame(skin.Groups[0], clone.Groups[0]);
        Assert.Equal(skin.Groups[0].MeterIds, clone.Groups[0].MeterIds);
        Assert.Equal(skin.Groups[0].Variables, clone.Groups[0].Variables);

        clone.Groups[0].MeterIds.Add("meter-2");
        clone.Groups[0].Variables["accent"] = "#000";

        Assert.Single(skin.Groups[0].MeterIds);
        Assert.Equal("#fff", skin.Groups[0].Variables["accent"]);
    }

    [Fact]
    public void Validator_RejectsInvalidGroupMembershipAndTransforms()
    {
        var skin = new SkinDefinition
        {
            Meters =
            {
                new MeterDefinition { Id = "meter-1", Width = 20, Height = 20 },
                new MeterDefinition { Id = "meter-2", Width = 20, Height = 20 },
            },
            Groups =
            {
                new WidgetGroupDefinition
                {
                    Id = "a",
                    Width = 0,
                    Height = 20,
                    MeterIds = { "meter-1", "missing" }
                },
                new WidgetGroupDefinition
                {
                    Id = "b",
                    Width = 20,
                    Height = 20,
                    MeterIds = { "meter-1" }
                }
            }
        };

        var issues = WidgetDefinitionValidator.Validate(skin);

        Assert.Contains(issues, i => i.Path == "groups.a.size");
        Assert.Contains(issues, i => i.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, i => i.Message.Contains("more than one group", StringComparison.OrdinalIgnoreCase));
    }
}
