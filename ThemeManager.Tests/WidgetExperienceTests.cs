using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetExperienceTests
{
    [Fact]
    public void GridLayout_AssignsStableRowsAndColumns()
    {
        var skin = new SkinDefinition { Width = 400, Height = 300 };
        for (var i = 0; i < 5; i++)
            skin.Meters.Add(new MeterDefinition { Id = $"m{i}", Width = 10, Height = 10, ZIndex = i });

        WidgetLayoutEngine.Apply(skin, new WidgetLayoutDefinition
        {
            Mode = WidgetLayoutMode.Grid,
            Padding = 10,
            Gap = 5,
            Columns = 2,
            CellWidth = 80,
            CellHeight = 30,
        });

        Assert.Equal((10, 10), (skin.Meters[0].X, skin.Meters[0].Y));
        Assert.Equal((95, 10), (skin.Meters[1].X, skin.Meters[1].Y));
        Assert.Equal((10, 45), (skin.Meters[2].X, skin.Meters[2].Y));
        Assert.Equal(80, skin.Meters[4].Width);
        Assert.Equal(30, skin.Meters[4].Height);
    }

    [Fact]
    public void RadialLayout_DistributesMetersAroundCenter()
    {
        var skin = new SkinDefinition { Width = 200, Height = 200 };
        for (var i = 0; i < 4; i++)
            skin.Meters.Add(new MeterDefinition { Id = $"m{i}", Width = 20, Height = 20 });

        WidgetLayoutEngine.Apply(skin, new WidgetLayoutDefinition
        {
            Mode = WidgetLayoutMode.Radial,
            Radius = 50,
            StartAngleDegrees = -90,
        });

        Assert.Equal(90, skin.Meters[0].X, 5);
        Assert.Equal(40, skin.Meters[0].Y, 5);
        Assert.Equal(140, skin.Meters[1].X, 5);
        Assert.Equal(90, skin.Meters[1].Y, 5);
    }

    [Fact]
    public void Animation_ReachesTargetAtOrAfterDuration()
    {
        var animation = new WidgetAnimation(0, 100, TimeSpan.FromMilliseconds(250));
        Assert.Equal(0, animation.Sample(TimeSpan.Zero));
        Assert.Equal(100, animation.Sample(TimeSpan.FromMilliseconds(250)));
        Assert.Equal(100, animation.Sample(TimeSpan.FromMilliseconds(500)));
    }
}
