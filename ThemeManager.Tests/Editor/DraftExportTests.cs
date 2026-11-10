using System.IO.Compression;
using System.Text.Json;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using ThemeManager.WinUI.ViewModels;

public sealed class DraftExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ThemedAI-draft-export", Guid.NewGuid().ToString("N"));
    public DraftExportTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private static SkinEditorViewModel Editor(SkinManagerService manager, bool newDraft = true)
    {
        var vm = new SkinEditorViewModel(manager); vm.LoadSkin(SkinDefaults.CreateClock(), newDraft); return vm;
    }
    [Fact] public async Task ExportDoesNotSaveEnableOrClearDirtyHistory()
    {
        var manager = new SkinManagerService(); var vm = Editor(manager); vm.Name = "Draft export";
        var history = vm.HistoryStatus; var path = Path.Combine(_root, "draft.themedwidget");
        Assert.True(await vm.ExportDraftAsync(path)); Assert.True(vm.Dirty); Assert.False(vm.CanDelete); Assert.Null(manager.Saved); Assert.Equal(history, vm.HistoryStatus);
        using var zip = ZipFile.OpenRead(path); using var json = JsonDocument.Parse(zip.GetEntry("skin.json")!.Open());
        Assert.Equal("Draft export", json.RootElement.GetProperty("Name").GetString()); Assert.False(json.RootElement.GetProperty("Enabled").GetBoolean());
        vm.Undo(); Assert.Equal("Cozy Clock", vm.Name); Assert.True(vm.Dirty);
    }
    [Fact] public async Task ExportOfCleanDraftDoesNotMarkItDirty()
    {
        var vm = Editor(new(), false); Assert.True(await vm.ExportDraftAsync(Path.Combine(_root, "clean.themedwidget")));
        Assert.False(vm.Dirty); Assert.False(vm.CanUndo);
    }
    [Fact] public async Task CancellationPreservesExistingPackageAndDraft()
    {
        var vm = Editor(new()); var path = Path.Combine(_root, "existing.themedwidget"); await File.WriteAllTextAsync(path, "existing content");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.False(await vm.ExportDraftAsync(path, cancellation.Token)); Assert.Equal("existing content", await File.ReadAllTextAsync(path)); Assert.True(vm.Dirty);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }
    [Fact] public async Task InvalidDraftExportRetainsEditsAndCreatesNoPackage()
    {
        var vm = Editor(new()); vm.Meters[0].MeasureName = "missing measure";
        var path = Path.Combine(_root, "invalid.themedwidget"); Assert.False(await vm.ExportDraftAsync(path));
        Assert.False(File.Exists(path)); Assert.True(vm.Dirty); Assert.True(vm.CanUndo);
    }
    [Fact] public async Task MissingImageExportRetainsOriginalDestination()
    {
        var vm = Editor(new()); vm.AddMeter(MeterKind.Image); vm.SelectedMeter!.ImagePath = Path.Combine(_root, "missing.png");
        var path = Path.Combine(_root, "existing.themedwidget"); await File.WriteAllTextAsync(path, "existing");
        Assert.False(await vm.ExportDraftAsync(path)); Assert.Equal("existing", await File.ReadAllTextAsync(path)); Assert.True(vm.Dirty);
    }
    [Fact] public async Task BlankDraftCanBeSavedButIsNotPersistedAtCreation()
    {
        var manager = new SkinManagerService(); var vm = new SkinEditorViewModel(manager);
        vm.LoadSkin(new SkinDefinition { Name = "New Widget", Enabled = false }, true);
        Assert.Null(manager.Saved); Assert.True(vm.Dirty); Assert.False(vm.CanDelete);
        Assert.True(await vm.SaveAsync()); Assert.True(manager.CreatedNew); Assert.Empty(manager.Saved!.Meters);
    }
}
