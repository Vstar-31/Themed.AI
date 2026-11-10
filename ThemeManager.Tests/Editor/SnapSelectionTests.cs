using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public sealed class SnapSelectionTests
{
    private static SkinEditorViewModel Editor()
    {
        var vm = new SkinEditorViewModel(new SkinManagerService());
        vm.LoadSkin(new SkinDefinition { Meters = new() { new() { Id = "a", X = 17, Y = -14 }, new() { Id = "b", X = 32, Y = 48 } } });
        return vm;
    }
    [Fact] public void SnapIsOneUndoStepAndRedoRestoresBoth()
    {
        var vm = Editor(); Assert.True(vm.SnapSelectedMeters(vm.Meters));
        Assert.Equal(20, vm.Meters[0].X); Assert.Equal(-10, vm.Meters[0].Y); Assert.Equal(30, vm.Meters[1].X);
        vm.Undo(); Assert.Equal(17, vm.Meters[0].X); Assert.Equal(48, vm.Meters[1].Y); Assert.False(vm.Dirty); Assert.False(vm.CanUndo);
        vm.Redo(); Assert.Equal(50, vm.Meters[1].Y);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidGridDoesNotEdit(double size)
    { var vm = Editor(); Assert.False(vm.SnapSelectedMeters(vm.Meters, size)); Assert.False(vm.Dirty); }
    [Fact] public void StaleSelectionRejectsWholeOperation()
    { var vm = Editor(); var stale = vm.Meters[0]; vm.Name = "Changed"; vm.Undo(); Assert.False(vm.SnapSelectedMeters(new[] { stale, vm.Meters[1] })); Assert.False(vm.Dirty); }
    [Fact] public void MixedParentsRejectWholeOperation()
    { var vm = Editor(); vm.GroupMeters(new[] { vm.Meters[0] }); var before = vm.Meters[0].X; Assert.False(vm.SnapSelectedMeters(vm.Meters)); Assert.Equal(before, vm.Meters[0].X); }
    [Fact] public void RecoverySnapshotIsIndependent()
    { var vm = Editor(); var copy = vm.CaptureRecoveryDraft(); copy.Meters[0].X = 999; Assert.Equal(17, vm.Meters[0].X); Assert.False(vm.Dirty); }
    [Fact] public void NoOpDoesNotAddHistory()
    { var vm = Editor(); vm.SnapSelectedMeters(vm.Meters); Assert.False(vm.SnapSelectedMeters(vm.Meters)); vm.Undo(); Assert.False(vm.CanUndo); }
}
