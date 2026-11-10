using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public sealed class KeyboardRecoveryTests
{
    private static SkinDefinition Fixture() => new()
    {
        Id = "source", Name = "Design", Width = 400, Height = 300,
        Meters = new()
        {
            new() { Id = "a", X = 10, Y = 20, StaticText = "First" },
            new() { Id = "b", X = 70, Y = 80, StaticText = "Second" }
        }
    };

    private static SkinEditorViewModel Editor(SkinDefinition? skin = null, SkinManagerService? manager = null)
    {
        var vm = new SkinEditorViewModel(manager ?? new()); vm.LoadSkin(skin ?? Fixture()); return vm;
    }

    [Fact] public void MultipleMetersMoveTogetherAsOneUndoStep()
    {
        var vm = Editor();
        Assert.True(vm.NudgeMeters(vm.Meters, -11, 10));
        Assert.Equal(-1, vm.Meters[0].X); Assert.Equal(59, vm.Meters[1].X);
        Assert.Equal(30, vm.Meters[0].Y); Assert.Equal(90, vm.Meters[1].Y);
        vm.Undo(); Assert.Equal(10, vm.Meters[0].X); Assert.Equal(70, vm.Meters[1].X);
        Assert.False(vm.Dirty); Assert.False(vm.CanUndo);
        vm.Redo(); Assert.Equal(-1, vm.Meters[0].X);
    }

    [Fact] public void GroupMovementKeepsChildGeometryAndTransform()
    {
        var skin = Fixture();
        skin.Groups.Add(new() { Id = "g", X = 40, Y = 50, Rotation = 30, ScaleX = 2, MeterIds = new() { "a", "b" } });
        var vm = Editor(skin);
        Assert.True(vm.NudgeLayer("group:g", 10, -1));
        Assert.Equal(50, vm.Groups[0].X); Assert.Equal(49, vm.Groups[0].Y);
        Assert.Equal(30, vm.Groups[0].Definition.Rotation); Assert.Equal(2, vm.Groups[0].Definition.ScaleX);
        Assert.Equal(10, vm.Meters[0].X); Assert.Equal(20, vm.Meters[0].Y);
        vm.Undo(); Assert.Equal(40, vm.Groups[0].X); Assert.False(vm.CanUndo);
    }

    [Fact] public void GroupChildMovesInLocalCoordinates()
    {
        var skin = Fixture(); skin.Groups.Add(new() { Id = "g", X = 40, ScaleX = 2, MeterIds = new() { "a" } });
        var vm = Editor(skin); Assert.True(vm.NudgeLayer("meter:a", 1, 0));
        Assert.Equal(11, vm.Meters[0].X); Assert.Equal(40, vm.Groups[0].X); Assert.Equal(70, vm.Meters[1].X);
    }

    [Fact] public void MixedParentsRejectWholeMove()
    {
        var skin = Fixture(); skin.Groups.Add(new() { Id = "g", MeterIds = new() { "a" } });
        var vm = Editor(skin); Assert.False(vm.NudgeMeters(vm.Meters, 1, 0));
        Assert.Equal(10, vm.Meters[0].X); Assert.Equal(70, vm.Meters[1].X); Assert.False(vm.Dirty);
    }

    [Fact] public void StaleSelectionRejectsWholeMove()
    {
        var vm = Editor(); var stale = vm.Meters[0]; vm.Name = "Change"; vm.Undo();
        Assert.False(vm.NudgeMeters(new[] { stale, vm.Meters[1] }, 1, 0));
        Assert.Equal(70, vm.Meters[1].X); Assert.True(vm.CanRedo); Assert.False(vm.Dirty);
    }

    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void NonFiniteMovesDoNotChangeDraft(double amount)
    {
        var vm = Editor(); Assert.False(vm.NudgeMeters(vm.Meters, amount, 1));
        Assert.Equal(10, vm.Meters[0].X); Assert.Equal(80, vm.Meters[1].Y); Assert.False(vm.CanUndo);
    }

    [Fact] public void OverflowIsRejectedBeforeMovingAnyMeter()
    {
        var skin = Fixture(); skin.Meters[1].X = double.MaxValue;
        var vm = Editor(skin); Assert.False(vm.NudgeMeters(vm.Meters, double.MaxValue, 1));
        Assert.Equal(10, vm.Meters[0].X); Assert.Equal(20, vm.Meters[0].Y); Assert.False(vm.Dirty);
    }

    [Fact] public void DuplicateSelectionMovesOnlyOnceAndNoOpsPreserveRedo()
    {
        var vm = Editor(); var meter = vm.Meters[0];
        Assert.True(vm.NudgeMeters(new[] { meter, meter }, 1, 0)); Assert.Equal(11, meter.X);
        vm.Undo(); Assert.False(vm.NudgeLayer("meter:a", 0, 0));
        Assert.False(vm.NudgeLayer("meter:missing", 1, 0)); Assert.False(vm.NudgeLayer("invalid", 1, 0));
        Assert.True(vm.CanRedo); Assert.False(vm.Dirty);
    }

    [Fact] public void RecoveryCannotReplaceAHealthyDraft()
    {
        var vm = Editor(); vm.Name = "Changed";
        Assert.False(vm.CanRecoverAsCopy); Assert.False(vm.RecoverAsNewDraft(Array.Empty<string>()));
        Assert.Equal("Changed", vm.Name); Assert.True(vm.CanUndo); Assert.True(vm.CanDelete);
    }

    [Fact] public async Task MissingSourceRecoversCurrentEditsWithNewIdentityAndExplicitSave()
    {
        var manager = new SkinManagerService { SaveError = new InvalidOperationException("removed") };
        var vm = Editor(manager: manager); vm.Meters[0].StaticText = "Keep me";
        Assert.False(await vm.SaveAsync()); Assert.True(vm.CanRecoverAsCopy);
        vm.Meters[1].X = 99;
        Assert.True(vm.RecoverAsNewDraft(new[] { "Design remix" }));
        Assert.Equal("Design remix 2", vm.Name); Assert.Equal("Keep me", vm.Meters[0].StaticText);
        Assert.Equal(99, vm.Meters[1].X); Assert.NotEqual("a", vm.Meters[0].Definition.Id);
        Assert.True(vm.Dirty); Assert.False(vm.CanDelete); Assert.False(vm.CanRecoverAsCopy);
        Assert.False(vm.CanUndo); Assert.Null(manager.Saved);
        manager.SaveError = null; Assert.True(await vm.SaveAsync());
        Assert.True(manager.CreatedNew); Assert.NotEqual("source", manager.Saved!.Id); Assert.False(vm.Dirty);
    }

    [Fact] public async Task RecoveryRetainsStatesAnchorsAndGroupReferences()
    {
        var skin = Fixture(); skin.Meters[0].States.Add(new() { Id = "state", Scale = 1.2 });
        skin.Anchor = new() { Edge = WidgetAnchorEdge.TopRight, MarginX = 42 };
        skin.Groups.Add(new() { Id = "g", MeterIds = new() { "a" } });
        var manager = new SkinManagerService { SaveError = new InvalidOperationException("removed") };
        var vm = Editor(skin, manager); Assert.False(await vm.SaveAsync());
        Assert.True(vm.RecoverAsNewDraft(Array.Empty<string>()));
        Assert.Equal(1.2, vm.Meters[0].Definition.States[0].Scale);
        Assert.NotEqual("state", vm.Meters[0].Definition.States[0].Id);
        Assert.Equal(vm.Meters[0].Definition.Id, Assert.Single(vm.Groups[0].Definition.MeterIds));
        Assert.NotEqual("g", vm.Groups[0].Definition.Id);
        manager.SaveError = null; Assert.True(await vm.SaveAsync());
        Assert.Equal(WidgetAnchorEdge.TopRight, manager.Saved!.Anchor!.Edge);
        Assert.Equal(42, manager.Saved.Anchor.MarginX);
    }

    [Fact] public async Task StorageFailureDoesNotOfferIdentityRecovery()
    {
        var vm = Editor(manager: new() { SaveError = new IOException("disk full") });
        vm.Name = "Keep me"; Assert.False(await vm.SaveAsync());
        Assert.False(vm.CanRecoverAsCopy); Assert.Equal("Keep me", vm.Name); Assert.True(vm.Dirty);
    }

    [Fact] public async Task LoadingAnotherDraftAndSuccessfulRetryClearRecovery()
    {
        var manager = new SkinManagerService { SaveError = new InvalidOperationException("removed") };
        var vm = Editor(manager: manager); Assert.False(await vm.SaveAsync());
        vm.LoadSkin(Fixture()); Assert.False(vm.CanRecoverAsCopy);
        Assert.False(await vm.SaveAsync()); manager.SaveError = null;
        Assert.True(await vm.SaveAsync()); Assert.False(vm.CanRecoverAsCopy);
    }
}
