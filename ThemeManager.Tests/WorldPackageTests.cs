using System.IO.Compression;
using System.Text.Json;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;
public sealed class WorldPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "world-package-" + Guid.NewGuid().ToString("N"));
    public WorldPackageTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private WorldPackageService Service => new(Path.Combine(root, "assets"));
    private string Package => Path.Combine(root, "test.themedworld");
    private static DesktopScene Scene() => new() { Name = "Original", Widgets = new() { new() { WidgetId = "source", X = 101, Y = 202, Definition = new() { Id = "source", Meters = new() { new() { Id = "meter", StaticText = "Hello" } } } } } };

    [Fact] public async Task RoundTripUsesNewIdentitiesAndDoesNotActivateOrMutateSource()
    {
        var scene = Scene(); var theme = CozyDefaults.CreateDefault();
        await Service.ExportAsync(scene, Array.Empty<SkinDefinition>(), theme, Package);
        var first = await Service.ImportAsync(Package); var second = await Service.ImportAsync(Package);
        Assert.NotEqual(scene.Id, first.Id); Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual("source", first.Widgets[0].WidgetId);
        Assert.NotEqual(first.Widgets[0].WidgetId, second.Widgets[0].WidgetId);
        Assert.False(first.Widgets[0].Definition!.Enabled); Assert.Equal(101, first.Widgets[0].X);
        Assert.Equal("Hello", first.Widgets[0].Definition!.Meters[0].StaticText);
        Assert.NotNull(first.ThemeSnapshot); Assert.NotEqual(theme.Id, first.ThemeSnapshot!.Id);
        Assert.Equal("source", scene.Widgets[0].Definition!.Id); Assert.Null(scene.ThemeSnapshot);
        var repository = new DesktopSceneRepository(Path.Combine(root, "storage"));
        var service = new DesktopSceneService(repository);
        var original = new DesktopScene { Name = "Already here" };
        await service.UpsertAsync(original); service.SetActiveScene(original);
        await service.UpsertAsync(first);
        Assert.Same(original, service.ActiveScene); Assert.Equal(2, service.Scenes.Count);
    }
    [Fact] public async Task FirstImportedWorldStaysInactiveAfterRestartUntilSelected()
    {
        await Service.ExportAsync(Scene(), Array.Empty<SkinDefinition>(), null, Package);
        var imported = await Service.ImportAsync(Package);
        var repository = new DesktopSceneRepository(Path.Combine(root, "restart"));
        var service = new DesktopSceneService(repository);
        await service.UpsertAsync(imported);
        var restarted = new DesktopSceneService(repository); await restarted.InitializeAsync();
        Assert.Null(restarted.ActiveScene);
        restarted.SetActiveScene(restarted.Scenes[0]); await restarted.FlushAsync();
        var selected = new DesktopSceneService(repository); await selected.InitializeAsync();
        Assert.Equal(imported.Id, selected.ActiveScene!.Id);
    }
    [Fact] public async Task BundlesImagesWallpaperAndStripsConnections()
    {
        var scene = Scene(); var skin = scene.Widgets[0].Definition!;
        var image = Path.Combine(root, "image.png"); await File.WriteAllBytesAsync(image, new byte[] { 1, 2, 3 });
        scene.WallpaperPath = image; skin.Meters[0].ImagePath = image;
        skin.Measures.Add(new() { Name = "Secret", Type = MeasureType.WebJson, Target = "secret" });
        await Service.ExportAsync(scene, Array.Empty<SkinDefinition>(), null, Package);
        File.Delete(image);
        var result = await Service.ImportAsync(Package);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(result.WallpaperPath!));
        Assert.True(File.Exists(result.Widgets[0].Definition!.Meters[0].ImagePath));
        Assert.Null(result.Widgets[0].Definition!.Measures[0].Target);
    }
    [Theory] [InlineData("../escape")] [InlineData("world.json")] [InlineData("extra.txt")]
    public async Task RejectsTraversalDuplicatesAndUnexpectedEntries(string name)
    {
        await Service.ExportAsync(Scene(), Array.Empty<SkinDefinition>(), null, Package);
        using (var zip = ZipFile.Open(Package, ZipArchiveMode.Update)) zip.CreateEntry(name);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
    }
    [Fact] public async Task MissingWidgetDoesNotReplaceExistingExport()
    {
        await File.WriteAllTextAsync(Package, "preserve"); var scene = Scene(); scene.Widgets[0].Definition = null;
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ExportAsync(scene, Array.Empty<SkinDefinition>(), null, Package));
        Assert.Equal("preserve", await File.ReadAllTextAsync(Package));
    }
    [Fact] public async Task BrokenNestedPackageCleansPartialImport()
    {
        await Service.ExportAsync(Scene(), Array.Empty<SkinDefinition>(), null, Package);
        using (var zip = ZipFile.Open(Package, ZipArchiveMode.Update))
        { zip.GetEntry("widgets/000.themedwidget")!.Delete(); using var output = new StreamWriter(zip.CreateEntry("widgets/000.themedwidget").Open()); output.Write("broken"); }
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "assets")));
    }
    [Fact] public async Task FailedLibrarySaveRemovesImportedAssetsAndKeepsExistingWorld()
    {
        await Service.ExportAsync(Scene(), Array.Empty<SkinDefinition>(), null, Package);
        var storage = Path.Combine(root, "failed-save");
        var library = new DesktopSceneService(new DesktopSceneRepository(storage));
        var existing = new DesktopScene(); await library.UpsertAsync(existing);
        Directory.CreateDirectory(Path.Combine(storage, "scenes.json.tmp"));
        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => Service.ImportToLibraryAsync(Package, library));
        Assert.Same(existing, Assert.Single(library.Scenes));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "assets")));
    }
    [Theory] [InlineData(99)] [InlineData(0)]
    public async Task RejectsUnsupportedWorldSchema(int version)
    {
        var scene = Scene(); scene.SchemaVersion = version;
        using(var zip=ZipFile.Open(Package,ZipArchiveMode.Create))
        { using var output=new StreamWriter(zip.CreateEntry("world.json").Open()); output.Write(JsonSerializer.Serialize(scene)); }
        await Assert.ThrowsAsync<InvalidDataException>(()=>Service.ImportAsync(Package));
    }
}
