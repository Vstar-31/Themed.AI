using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public sealed class EditorSaveStatusTests
{
    [Fact] public async Task PendingSaveRejectsSecondSaveAndPreservesNewEdits()
    {
        var manager = new SkinManagerService { SaveGate = new TaskCompletionSource() };
        var vm = new SkinEditorViewModel(manager); vm.LoadSkin(new SkinDefinition { Name = "First" }); vm.Name = "Checkpoint";
        var pending = vm.SaveAsync(); Assert.True(vm.IsSaving); Assert.Equal("Saving widget…", vm.SaveState);
        vm.Name = "Newer"; Assert.False(await vm.SaveAsync());
        manager.SaveGate.SetResult(); Assert.True(await pending); Assert.False(vm.IsSaving);
        Assert.Equal("Checkpoint", manager.Saved!.Name); Assert.True(vm.Dirty); Assert.Equal("Unsaved edits", vm.SaveState);
    }
    [Fact] public async Task FailedSaveCanBeRetried()
    {
        var manager = new SkinManagerService { SaveError = new IOException("disk full") };
        var vm = new SkinEditorViewModel(manager); vm.LoadSkin(new SkinDefinition { Name = "First" }); vm.Name = "Unsaved";
        Assert.False(await vm.SaveAsync()); Assert.False(vm.IsSaving); Assert.True(vm.Dirty);
        manager.SaveError = null; Assert.True(await vm.SaveAsync()); Assert.Equal("Saved", vm.SaveState);
    }
}
