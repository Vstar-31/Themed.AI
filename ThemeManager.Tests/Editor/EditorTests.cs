using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public class EditorTests
{
    private static SkinDefinition Fixture() => new()
    {
        Name = "Editor fixture", Width = 400, Height = 300,
        Measures = new() { new() { Name = "Load", Type = MeasureType.Cpu } },
        Meters = new()
        {
            new() { Id = "a", X = 10, Y = 20, Width = 30, Height = 20, ZIndex = 0, StaticText = "A", MeasureName = "Load", States = new() { new() { Id = "state-a", Scale = 1.2 } } },
            new() { Id = "b", X = 70, Y = 80, Width = 40, Height = 30, ZIndex = 3, StaticText = "B" },
            new() { Id = "c", X = 150, Y = 140, Width = 50, Height = 40, ZIndex = 8, StaticText = "C" },
            new() { Id = "d", X = 250, Y = 200, Width = 60, Height = 50, ZIndex = 5, StaticText = "D" }
        }
    };
    private static SkinDefinition Grouped()
    {
        var skin = Fixture();
        skin.Groups.Add(new() { Id = "g", Name = "Pair", X = 20, Y = 30, ScaleX = 1.4, Rotation = 12, MeterIds = new() { "a", "b" }, Variables = new() { ["accent"] = "blue" } });
        return skin;
    }
    private static SkinEditorViewModel Editor(SkinDefinition? skin = null, SkinManagerService? manager = null)
    {
        var vm = new SkinEditorViewModel(manager ?? new()); vm.LoadSkin(skin ?? Fixture()); return vm;
    }
    private static string[] RootOrder(SkinEditorViewModel vm) => vm.GetRootLayers().Select(l => l.Key).ToArray();

    [Fact] public void LoadingIsAnIndependentCleanDraft()
    {
        var skin = Fixture(); var vm = Editor(skin);
        Assert.False(vm.Dirty); Assert.False(vm.CanUndo); Assert.False(vm.CanRedo);
        vm.Meters[0].StaticText = "Changed";
        Assert.Equal("A", skin.Meters[0].StaticText);
        vm.Undo(); Assert.Equal("A", vm.Meters[0].StaticText); Assert.False(vm.Dirty);
        vm.Redo(); Assert.Equal("Changed", vm.Meters[0].StaticText); Assert.True(vm.Dirty);
    }
    [Fact] public void BranchingAfterUndoDiscardsRedo()
    {
        var vm = Editor(); vm.Name = "One"; vm.Name = "Two"; vm.Undo(); vm.Width = 500;
        Assert.False(vm.CanRedo); vm.Undo(); Assert.Equal(400, vm.Width); Assert.Equal("One", vm.Name);
    }
    [Fact] public void CompoundMovementIsOneUndoStep()
    {
        var vm = Editor(); using(vm.BeginEdit()) { for(int i = 0; i < 20; i++) vm.Meters[0].MoveTo(i * 10, i * 5); }
        vm.Undo(); Assert.Equal(10, vm.Meters[0].X); Assert.Equal(20, vm.Meters[0].Y); Assert.False(vm.CanUndo);
        vm.Redo(); Assert.Equal(190, vm.Meters[0].X);
    }
    [Fact] public void NestedAndNoOpTransactionsDoNotAddExtraHistory()
    {
        var vm = Editor(); using(vm.BeginEdit()) { using(vm.BeginEdit()) { vm.Width = 500; } vm.Height = 200; }
        vm.Undo(); Assert.False(vm.CanUndo); Assert.False(vm.Dirty);
        using(vm.BeginEdit()) { vm.Width = vm.Width; } Assert.True(vm.CanRedo);
    }
    [Fact] public void HistoryRetainsAtMost100Steps()
    {
        var vm = Editor(); for(int i = 0; i < 130; i++) vm.Name = "Edit " + i;
        int undone = 0; while(vm.CanUndo) { vm.Undo(); undone++; }
        Assert.Equal(100, undone); Assert.Equal("Edit 29", vm.Name); Assert.True(vm.Dirty);
    }
    [Fact] public void PreviewInputsAreNotDraftChanges()
    {
        var vm = Editor(); vm.PreviewInteractions = true; vm.PreviewActive = true; vm.PreviewMeasureValue = 75;
        vm.PreviewAreaWidth = 1280; Assert.False(vm.Dirty); Assert.False(vm.CanUndo);
    }
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void NonFiniteGeometryDoesNotCorruptDraft(double value)
    {
        var vm = Editor(); vm.Width = value; vm.Meters[0].X = value;
        Assert.Equal(400, vm.Width); Assert.Equal(10, vm.Meters[0].X); Assert.False(vm.Dirty);
    }
    [Fact] public void RootOrderingUsesMaximumGroupMember()
    {
        var vm = Editor(Grouped()); Assert.Equal(new[] {"group:g", "meter:d", "meter:c"}, RootOrder(vm));
        vm.ArrangeLayer("group:g", "Front"); Assert.Equal(new[] {"meter:d", "meter:c", "group:g"}, RootOrder(vm));
        Assert.Equal(new[] {"a", "b"}, vm.Groups[0].Definition.MeterIds);
        vm.Undo(); Assert.Equal(new[] {"group:g", "meter:d", "meter:c"}, RootOrder(vm)); Assert.False(vm.CanUndo);
    }
    [Fact] public void TiedRootLayersMatchHostInsertionOrder()
    {
        var skin = Grouped(); foreach(var meter in skin.Meters) meter.ZIndex = 0;
        Assert.Equal(new[] {"group:g", "meter:c", "meter:d"}, RootOrder(Editor(skin)));
    }
    [Fact] public void ChildOrderingPreservesRootOrder()
    {
        var vm = Editor(Grouped()); var roots = RootOrder(vm);
        vm.ArrangeLayer("meter:a", "Front"); Assert.Equal(roots, RootOrder(vm));
        Assert.Equal(new[] {"meter:b", "meter:a"}, vm.GetRootLayers()[0].Children.Select(c => c.Key));
        vm.Undo(); Assert.Equal(new[] {"meter:a", "meter:b"}, vm.GetRootLayers()[0].Children.Select(c => c.Key));
    }
    [Fact] public void RootMeterCanMoveAcrossGroup()
    {
        var vm = Editor(Grouped()); vm.ArrangeLayer("meter:d", "Back");
        Assert.Equal(new[] {"meter:d", "group:g", "meter:c"}, RootOrder(vm));
    }
    [Fact] public void LayerBoundaryIsNotAnUndoStep()
    {
        var vm = Editor(Grouped()); vm.ArrangeLayer("group:g", "Back"); Assert.False(vm.Dirty); Assert.False(vm.CanUndo);
    }
    [Fact] public void ExtremeZValuesCanBeReorderedWithoutOverflow()
    {
        var skin = Fixture(); skin.Meters[0].ZIndex = int.MinValue; skin.Meters[1].ZIndex = int.MaxValue;
        var vm = Editor(skin); vm.ArrangeLayer("meter:a", "Front"); Assert.Equal("meter:a", RootOrder(vm)[^1]);
        Assert.Equal(4, vm.Meters.Select(m => m.Definition.ZIndex).Distinct().Count());
    }
    [Fact] public void MeterDuplicationIsIndependentAndUndoable()
    {
        var vm = Editor(Grouped()); string key = vm.DuplicateLayer("meter:a")!;
        var copy = vm.Meters.Single(m => "meter:" + m.Definition.Id == key);
        Assert.Equal(5, vm.Meters.Count); Assert.Contains(copy.Definition.Id, vm.Groups[0].Definition.MeterIds);
        Assert.Equal("Load", copy.MeasureName); Assert.Equal(26, copy.X);
        Assert.NotEqual("state-a", copy.Definition.States[0].Id);
        copy.Definition.States[0].Scale = 2; Assert.Equal(1.2, vm.Meters[0].Definition.States[0].Scale);
        vm.Undo(); Assert.Equal(4, vm.Meters.Count); Assert.Equal(2, vm.Groups[0].Definition.MeterIds.Count); Assert.False(vm.CanUndo);
    }
    [Fact] public void GroupDuplicationRemapsMembershipAndPreservesTransforms()
    {
        var vm = Editor(Grouped()); string key = vm.DuplicateLayer("group:g")!;
        var copy = vm.Groups.Single(g => "group:" + g.Definition.Id == key);
        Assert.Equal(6, vm.Meters.Count); Assert.Equal(36, copy.X); Assert.Equal(1.4, copy.ScaleX); Assert.Equal(12, copy.Rotation);
        Assert.Empty(copy.Definition.MeterIds.Intersect(new[] {"a", "b"}));
        Assert.All(copy.Definition.MeterIds, id => Assert.Contains(vm.Meters, m => m.Definition.Id == id));
        copy.Definition.Variables["accent"] = "red"; Assert.Equal("blue", vm.Groups[0].Definition.Variables["accent"]);
        vm.Undo(); Assert.Single(vm.Groups); Assert.Equal(4, vm.Meters.Count); Assert.False(vm.CanUndo);
        vm.Redo(); Assert.Equal(2, vm.Groups.Count); Assert.Equal(6, vm.Meters.Count);
    }
    [Fact] public void GroupCopyNamesStayUnique()
    {
        var vm = Editor(Grouped()); vm.DuplicateLayer("group:g"); vm.DuplicateLayer("group:g");
        Assert.Equal(3, vm.Groups.Select(g => g.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
    [Fact] public void UndoKeepsGroupSelection()
    {
        var vm = Editor(Grouped()); vm.SelectedMeter = null; vm.SelectedGroup = vm.Groups[0];
        vm.ArrangeLayer("group:g", "Front"); vm.Undo(); Assert.Null(vm.SelectedMeter); Assert.Equal("g", vm.SelectedGroup!.Definition.Id);
    }
    [Theory] [InlineData("Left")] [InlineData("Right")] [InlineData("CenterX")] [InlineData("Top")] [InlineData("Bottom")] [InlineData("CenterY")]
    public void AlignmentHasOneUndoStep(string action)
    {
        var vm = Editor(); vm.AlignMeters(vm.Meters.Take(3), action);
        var meters = vm.Meters.Take(3).ToArray();
        double Value(MeterEditorItem m) => action switch { "Left" => m.X, "Right" => m.X + m.Width, "CenterX" => m.X + m.Width/2, "Top" => m.Y, "Bottom" => m.Y + m.Height, _ => m.Y + m.Height/2 };
        Assert.All(meters, m => Assert.Equal(Value(meters[0]), Value(m), 8));
        vm.Undo(); Assert.Equal(70, vm.Meters[1].X); Assert.Equal(80, vm.Meters[1].Y); Assert.False(vm.CanUndo);
    }
    [Theory] [InlineData("DistributeX")] [InlineData("DistributeY")]
    public void DistributionUsesEqualGapsForMixedSizes(string action)
    {
        var vm = Editor(); vm.AlignMeters(vm.Meters.Take(3), action);
        var m = vm.Meters; bool x = action == "DistributeX";
        double first = x ? m[1].X-m[0].X-m[0].Width : m[1].Y-m[0].Y-m[0].Height;
        double second = x ? m[2].X-m[1].X-m[1].Width : m[2].Y-m[1].Y-m[1].Height;
        Assert.Equal(first, second, 8); vm.Undo(); Assert.False(vm.CanUndo);
    }
    [Fact] public void CrossGroupAlignmentDoesNotMutateDraft()
    {
        var vm = Editor(Grouped()); vm.AlignMeters(new[] { vm.Meters[0], vm.Meters[2] }, "Left"); Assert.False(vm.CanUndo); Assert.False(vm.Dirty);
    }
    [Fact] public void RegroupingDoesNotLeaveEmptyContainers()
    {
        var vm = Editor(Grouped()); vm.GroupMeters(vm.Meters.Take(2)); Assert.Single(vm.Groups);
        vm.Undo(); Assert.Single(vm.Groups); Assert.Equal("g", vm.Groups[0].Definition.Id); Assert.False(vm.CanUndo);
    }
    [Fact] public void StatesAndAnchorsParticipateInHistory()
    {
        var vm = Editor(); vm.AddState(); Assert.Equal(2, vm.States.Count); vm.Undo(); Assert.Single(vm.States);
        vm.AnchorEnabled = true; vm.AnchorEditor!.MarginX = 42; vm.Undo(); Assert.NotEqual(42, vm.AnchorEditor.MarginX);
        vm.Undo(); Assert.False(vm.AnchorEnabled); Assert.False(vm.Dirty);
    }
    [Fact] public async Task SaveMarksBaselineAndPersistsIndependentDraft()
    {
        var manager = new SkinManagerService(); var vm = Editor(manager: manager); vm.Name = "New name";
        Assert.True(await vm.SaveAsync()); Assert.False(vm.Dirty); Assert.Equal("New name", manager.Saved!.Name);
        vm.Name = "Later"; vm.Undo(); Assert.False(vm.Dirty); Assert.Equal("New name", vm.Name);
    }
    [Fact] public async Task FailedSaveKeepsDraftAndHistory()
    {
        var manager = new SkinManagerService { SaveError = new IOException("fixture") }; var vm = Editor(manager: manager); vm.Name = "Unsaved";
        Assert.False(await vm.SaveAsync()); Assert.True(vm.Dirty); Assert.True(vm.CanUndo); Assert.Equal("Unsaved", vm.Name);
    }
}
