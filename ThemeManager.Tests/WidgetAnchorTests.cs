using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetAnchorTests
{
    // A 1920x1040 work area (taskbar below) offset from the origin, as on a second monitor.
    private static readonly WidgetRect Area = new(100, 50, 1920, 1040);

    private static WidgetLayout Place(WidgetAnchorEdge edge, double mx = 16, double my = 16,
        double w = 200, double h = 100, WidgetRect? area = null, WidgetResponsiveRule[]? rules = null)
    {
        var anchor = new WidgetAnchorDefinition { Edge = edge, MarginX = mx, MarginY = my };
        if (rules is not null) anchor.Responsive.AddRange(rules);
        return WidgetAnchorResolver.Resolve(anchor, area ?? Area, w, h, fallbackX: 7, fallbackY: 9);
    }

    [Theory]
    [InlineData(WidgetAnchorEdge.TopLeft, 116, 66)]
    [InlineData(WidgetAnchorEdge.Top, 100 + 860 + 16, 66)]          // 100 + (1920-200)/2 + 16
    [InlineData(WidgetAnchorEdge.TopRight, 100 + 1920 - 200 - 16, 66)]
    [InlineData(WidgetAnchorEdge.Left, 116, 50 + (1040 - 100) / 2 + 16)]
    [InlineData(WidgetAnchorEdge.Center, 100 + 860 + 16, 50 + 470 + 16)]
    [InlineData(WidgetAnchorEdge.Right, 100 + 1920 - 200 - 16, 50 + 470 + 16)]
    [InlineData(WidgetAnchorEdge.BottomLeft, 116, 50 + 1040 - 100 - 16)]
    [InlineData(WidgetAnchorEdge.Bottom, 100 + 860 + 16, 50 + 1040 - 100 - 16)]
    [InlineData(WidgetAnchorEdge.BottomRight, 100 + 1920 - 200 - 16, 50 + 1040 - 100 - 16)]
    public void Resolve_PlacesWidgetAtEachEdgeAndCorner(WidgetAnchorEdge edge, double x, double y)
    {
        var layout = Place(edge);
        Assert.Equal(x, layout.X, 6);
        Assert.Equal(y, layout.Y, 6);
        Assert.Equal(1.0, layout.Scale);
        Assert.False(layout.Hidden);
        Assert.Null(layout.ActiveRule);
    }

    [Fact]
    public void Resolve_NoEdgeKeepsFallbackPosition()
    {
        var layout = Place(WidgetAnchorEdge.None);
        Assert.Equal(7, layout.X);
        Assert.Equal(9, layout.Y);
    }

    [Fact]
    public void Resolve_ClampsMarginsThatWouldPushWidgetOutsideTheArea()
    {
        var layout = Place(WidgetAnchorEdge.TopLeft, mx: 5000, my: -5000);
        Assert.Equal(Area.Right - 200, layout.X);   // right-most that still fits
        Assert.Equal(Area.Y, layout.Y);             // top-most
    }

    [Fact]
    public void Resolve_WidgetLargerThanAreaPinsToAreaOrigin()
    {
        var layout = Place(WidgetAnchorEdge.BottomRight, w: 5000, h: 5000, area: new WidgetRect(0, 0, 800, 600));
        Assert.Equal(0, layout.X);
        Assert.Equal(0, layout.Y);
    }

    [Fact]
    public void Resolve_ScaleAffectsPlacementOfFarEdges()
    {
        var rule = new WidgetResponsiveRule { Scale = 0.5 };
        var layout = Place(WidgetAnchorEdge.BottomRight, rules: new[] { rule });
        // Scaled size is 100x50, so the far-edge position moves in by the freed space.
        Assert.Equal(Area.Right - 100 - 16, layout.X, 6);
        Assert.Equal(Area.Bottom - 50 - 16, layout.Y, 6);
        Assert.Equal(0.5, layout.Scale);
    }

    // ── Responsive rules ─────────────────────────────────────────────────────

    [Fact]
    public void SelectRule_NoRulesOrNoMatchReturnsNull()
    {
        var anchor = new WidgetAnchorDefinition();
        Assert.Null(WidgetAnchorResolver.SelectRule(anchor, Area));
        anchor.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = 1280 });
        Assert.Null(WidgetAnchorResolver.SelectRule(anchor, Area));
    }

    [Fact]
    public void SelectRule_LimitsAreInclusive()
    {
        var rule = new WidgetResponsiveRule { MaxWorkAreaWidth = 1920 };
        var anchor = new WidgetAnchorDefinition { Responsive = { rule } };
        Assert.Same(rule, WidgetAnchorResolver.SelectRule(anchor, Area));
        Assert.Null(WidgetAnchorResolver.SelectRule(anchor, Area with { Width = 1921 }));
    }

    [Fact]
    public void SelectRule_ChecksHeightIndependentlyOfWidth()
    {
        var rule = new WidgetResponsiveRule { MaxWorkAreaHeight = 800 };
        var anchor = new WidgetAnchorDefinition { Responsive = { rule } };
        Assert.Null(WidgetAnchorResolver.SelectRule(anchor, Area));
        Assert.Same(rule, WidgetAnchorResolver.SelectRule(anchor, Area with { Height = 768 }));
    }

    [Fact]
    public void SelectRule_MostSpecificSmallestLimitWinsRegardlessOfOrder()
    {
        var wide = new WidgetResponsiveRule { Id = "wide", MaxWorkAreaWidth = 2560 };
        var narrow = new WidgetResponsiveRule { Id = "narrow", MaxWorkAreaWidth = 1920 };
        var any = new WidgetResponsiveRule { Id = "any" };
        var anchor = new WidgetAnchorDefinition { Responsive = { any, wide, narrow } };

        Assert.Same(narrow, WidgetAnchorResolver.SelectRule(anchor, Area));
        Assert.Same(wide, WidgetAnchorResolver.SelectRule(anchor, Area with { Width = 2500 }));
        Assert.Same(any, WidgetAnchorResolver.SelectRule(anchor, Area with { Width = 3840 }));
    }

    [Fact]
    public void SelectRule_TiesFallBackToHeightThenListOrder()
    {
        var a = new WidgetResponsiveRule { Id = "a", MaxWorkAreaWidth = 2000, MaxWorkAreaHeight = 1200 };
        var b = new WidgetResponsiveRule { Id = "b", MaxWorkAreaWidth = 2000, MaxWorkAreaHeight = 1100 };
        var c = new WidgetResponsiveRule { Id = "c", MaxWorkAreaWidth = 2000, MaxWorkAreaHeight = 1100 };
        var anchor = new WidgetAnchorDefinition { Responsive = { a, b, c } };
        Assert.Same(b, WidgetAnchorResolver.SelectRule(anchor, Area));
    }

    [Fact]
    public void Resolve_RuleOverridesEdgeMarginsScaleAndVisibility()
    {
        var rule = new WidgetResponsiveRule
        {
            MaxWorkAreaWidth = 1280, Edge = WidgetAnchorEdge.BottomLeft, MarginX = 4, MarginY = 8, Hidden = true,
        };
        var small = new WidgetRect(0, 0, 1280, 720);
        var layout = Place(WidgetAnchorEdge.TopRight, area: small, rules: new[] { rule });

        Assert.Same(rule, layout.ActiveRule);
        Assert.Equal(WidgetAnchorEdge.BottomLeft, layout.Edge);
        Assert.Equal(4, layout.X);
        Assert.Equal(720 - 100 - 8, layout.Y);
        Assert.True(layout.Hidden);

        // On a large monitor the same widget is unaffected by the rule.
        var large = Place(WidgetAnchorEdge.TopRight, rules: new[] { rule });
        Assert.Null(large.ActiveRule);
        Assert.Equal(WidgetAnchorEdge.TopRight, large.Edge);
        Assert.False(large.Hidden);
    }

    [Fact]
    public void Resolve_InvalidRuleScaleIsIgnored()
    {
        foreach (var bad in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
            Assert.Equal(1.0, Place(WidgetAnchorEdge.TopLeft, rules: new[] { new WidgetResponsiveRule { Scale = bad } }).Scale);
    }

    // ── Drag inverse ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WidgetAnchorEdge.TopLeft, 24, 40)]
    [InlineData(WidgetAnchorEdge.TopRight, 24, 40)]
    [InlineData(WidgetAnchorEdge.BottomLeft, 24, 40)]
    [InlineData(WidgetAnchorEdge.BottomRight, 24, 40)]
    [InlineData(WidgetAnchorEdge.Center, 24, 40)]
    [InlineData(WidgetAnchorEdge.Top, -30, 40)]
    [InlineData(WidgetAnchorEdge.Left, 24, -30)]
    public void MarginsFromPosition_InvertsResolve(WidgetAnchorEdge edge, double mx, double my)
    {
        var placed = Place(edge, mx, my);
        var (rx, ry) = WidgetAnchorResolver.MarginsFromPosition(edge, Area, 200, 100, 1.0, placed.X, placed.Y);
        Assert.Equal(mx, rx, 6);
        Assert.Equal(my, ry, 6);
    }

    [Fact]
    public void MarginsFromPosition_PinnedSidesNeverGoNegative_CenteredAxesMay()
    {
        var (mx, my) = WidgetAnchorResolver.MarginsFromPosition(
            WidgetAnchorEdge.TopLeft, Area, 200, 100, 1.0, Area.X - 50, Area.Y - 50);
        Assert.Equal(0, mx);
        Assert.Equal(0, my);

        var (cx, _) = WidgetAnchorResolver.MarginsFromPosition(
            WidgetAnchorEdge.Center, Area, 200, 100, 1.0, Area.X, Area.Y);
        Assert.True(cx < 0);

        Assert.Equal((0.0, 0.0), WidgetAnchorResolver.MarginsFromPosition(WidgetAnchorEdge.None, Area, 200, 100, 1.0, 5, 5));
    }

    [Fact]
    public void MarginsFromPosition_AccountsForScale()
    {
        var rule = new WidgetResponsiveRule { Scale = 0.5 };
        var placed = Place(WidgetAnchorEdge.BottomRight, 30, 20, rules: new[] { rule });
        var (mx, my) = WidgetAnchorResolver.MarginsFromPosition(WidgetAnchorEdge.BottomRight, Area, 200, 100, 0.5, placed.X, placed.Y);
        Assert.Equal(30, mx, 6);
        Assert.Equal(20, my, 6);
    }

    // ── Model plumbing ───────────────────────────────────────────────────────

    [Fact]
    public void Clone_DeepCopiesAnchorAndRules()
    {
        var skin = new SkinDefinition
        {
            Anchor = new WidgetAnchorDefinition
            {
                Edge = WidgetAnchorEdge.TopRight, Monitor = WidgetMonitorTarget.Primary, MarginX = 12,
                Responsive = { new WidgetResponsiveRule { Id = "r", MaxWorkAreaWidth = 1366, Scale = 0.8 } },
            },
        };

        var copy = skin.Clone("copy");
        copy.Anchor!.Responsive[0].Scale = 0.1;
        copy.Anchor.Edge = WidgetAnchorEdge.Center;

        Assert.NotSame(skin.Anchor, copy.Anchor);
        Assert.Equal(0.8, skin.Anchor!.Responsive[0].Scale);
        Assert.Equal(WidgetAnchorEdge.TopRight, skin.Anchor.Edge);
        Assert.Equal(WidgetMonitorTarget.Primary, copy.Anchor.Monitor);
    }

    [Fact]
    public void Clone_WithoutAnchorStaysNull() => Assert.Null(new SkinDefinition().Clone().Anchor);

    [Fact]
    public void ResetMarginsResetsResponsiveOverridesWithoutChangingRuleBehavior()
    {
        var anchor = new WidgetAnchorDefinition
        {
            Edge = WidgetAnchorEdge.BottomRight, MarginX = 40, MarginY = 50,
            Responsive = { new WidgetResponsiveRule { MaxWorkAreaWidth = 1200, MarginX = 80, Scale = 0.75 } }
        };
        anchor.ResetMargins();
        var layout = WidgetAnchorResolver.Resolve(anchor, new WidgetRect(0, 0, 1000, 800), 200, 100);
        Assert.Equal(16, layout.MarginX);
        Assert.Equal(16, layout.MarginY);
        Assert.Equal(0.75, layout.Scale);
        Assert.Null(anchor.Responsive[0].MarginY);
        Assert.Equal(834, layout.X);
        Assert.Equal(709, layout.Y);
    }

    [Fact]
    public void ResetMarginsAcceptsLegacyNullRules()
    {
        var anchor = new WidgetAnchorDefinition { MarginX = 99, MarginY = 88, Responsive = null! };
        anchor.ResetMargins();
        Assert.Equal(16, anchor.MarginX);
        Assert.Equal(16, anchor.MarginY);
    }
}
