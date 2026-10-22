using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public sealed class RemixDraftTests
{
    [Fact] public async Task NewRemixRemainsUnsavedUntilExplicitSave()
    {
        var manager = new SkinManagerService(); var vm = new SkinEditorViewModel(manager);
        vm.LoadSkin(WidgetRemix.Create(SkinDefaults.CreateClock()), newDraft: true);
        Assert.True(vm.Dirty); Assert.False(vm.CanDelete); Assert.Null(manager.Saved);
        vm.Name = "My clock"; vm.Undo(); Assert.True(vm.Dirty);
        Assert.True(await vm.SaveAsync()); Assert.True(manager.CreatedNew); Assert.True(vm.CanDelete); Assert.False(vm.Dirty);
    }
    [Fact] public async Task FailedNewDraftSaveDoesNotLoseNewDraftState()
    {
        var manager = new SkinManagerService { SaveError = new IOException("fixture") }; var vm = new SkinEditorViewModel(manager);
        vm.LoadSkin(WidgetRemix.Create(SkinDefaults.CreateClock()), true);
        Assert.False(await vm.SaveAsync()); Assert.True(vm.Dirty); Assert.False(vm.CanDelete);
    }
    [Fact] public async Task EditsMadeDuringSaveAreRetainedAsUnsaved()
    {
        var manager = new SkinManagerService { SaveGate = new TaskCompletionSource() }; var vm = new SkinEditorViewModel(manager);
        vm.LoadSkin(SkinDefaults.CreateClock()); vm.Name = "First";
        var save = vm.SaveAsync(); vm.Name = "Second"; vm.Width = 500;
        manager.SaveGate.SetResult(); Assert.True(await save);
        Assert.Equal("First", manager.Saved!.Name); Assert.Equal("Second", vm.Name); Assert.Equal(500, vm.Width); Assert.True(vm.Dirty);
        vm.Undo(); vm.Undo(); Assert.False(vm.Dirty); Assert.Equal("First", vm.Name);
    }
    [Fact] public async Task MissingExistingWidgetProducesRecoverableDraftError()
    {
        var manager = new SkinManagerService { SaveError = new InvalidOperationException("removed") }; var vm = new SkinEditorViewModel(manager);
        vm.LoadSkin(SkinDefaults.CreateClock()); vm.Name = "Edited";
        Assert.False(await vm.SaveAsync()); Assert.True(vm.Dirty); Assert.Equal("Edited", vm.Name);
    }
}
