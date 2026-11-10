using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public sealed class AssetEditorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ThemedAI-asset-editor", Guid.NewGuid().ToString("N"));
    private string Image => Path.Combine(_root, "test.png");
    public AssetEditorTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Image, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII="));
    }
    public void Dispose() => Directory.Delete(_root, true);
    private static SkinEditorViewModel Editor()
    {
        var vm = new SkinEditorViewModel(new SkinManagerService());
        vm.LoadSkin(new SkinDefinition { Meters = new() { new() { Id = "image", Kind = MeterKind.Image }, new() { Id = "text", Kind = MeterKind.String } } });
        return vm;
    }
    [Fact] public void AddingImageMeterAndAssigningAssetIsOneUndoStep()
    {
        var vm = Editor(); Assert.True(vm.ApplyImageAsset(Image, null));
        Assert.Equal(3, vm.Meters.Count); Assert.Equal(Image, vm.SelectedMeter!.ImagePath);
        vm.Undo(); Assert.Equal(2, vm.Meters.Count); Assert.False(vm.Dirty); Assert.False(vm.CanUndo);
        vm.Redo(); Assert.Equal(3, vm.Meters.Count); Assert.Equal(Image, vm.Meters.Last().ImagePath);
    }
    [Fact] public void ReplacementIsUndoableAndDoesNotAddMeter()
    {
        var vm = Editor(); Assert.True(vm.ApplyImageAsset(Image, "image")); Assert.Equal(2, vm.Meters.Count);
        vm.Undo(); Assert.Equal("", vm.Meters[0].ImagePath); Assert.False(vm.CanUndo);
    }
    [Theory] [InlineData("deleted")] [InlineData("text")]
    public void StaleOrWrongKindTargetDoesNotSilentlyCreateMeter(string id)
    {
        var vm = Editor(); Assert.False(vm.ApplyImageAsset(Image, id)); Assert.Equal(2, vm.Meters.Count); Assert.False(vm.Dirty);
    }
    [Fact] public void MissingAssetIsNotAssigned()
    {
        var vm = Editor(); Assert.False(vm.ApplyImageAsset(Path.Combine(_root, "missing.png"), "image")); Assert.False(vm.CanUndo);
    }
}
