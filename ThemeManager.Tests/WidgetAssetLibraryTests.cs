using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class WidgetAssetLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ThemedAI-assets", Guid.NewGuid().ToString("N"));
    private string Library => Path.Combine(_root, "managed");
    private WidgetAssetLibrary Service => new(Library);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=");
    public WidgetAssetLibraryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private async Task<string> Source(string name = "picture.png", byte[]? bytes = null)
    {
        var path = Path.Combine(_root, name); await File.WriteAllBytesAsync(path, bytes ?? Png); return path;
    }

    [Fact] public async Task ImportCopiesImageAndSurvivesSourceDeletion()
    {
        var source = await Source(); var asset = await Service.ImportAsync(source);
        Assert.StartsWith(Library, asset.Path); Assert.NotEqual(source, asset.Path); Assert.Equal(Png, await File.ReadAllBytesAsync(asset.Path));
        File.Delete(source); Assert.True(WidgetAssetLibrary.Inspect(asset.Path).Available); Assert.Equal("picture.png", asset.Name);
    }
    [Fact] public async Task IdenticalContentWithDifferentNamesReusesStoredCopy()
    {
        var first = await Service.ImportAsync(await Source("one.png"));
        var second = await Service.ImportAsync(await Source("two.png"));
        Assert.Equal(first.Path, second.Path); Assert.Single((await Service.ListAsync()).Items);
    }
    [Fact] public async Task ConcurrentImportsDoNotCreateDuplicateCopies()
    {
        var a = await Source("a.png"); var b = await Source("b.png");
        var results = await Task.WhenAll(Service.ImportAsync(a), Service.ImportAsync(b));
        Assert.Equal(results[0].Path, results[1].Path); Assert.Single((await Service.ListAsync()).Items);
    }
    [Fact] public async Task SameFilenameDifferentContentNeverOverwritesExistingAsset()
    {
        var source = await Source(); var first = await Service.ImportAsync(source);
        await File.WriteAllBytesAsync(source, Png.Concat(new byte[] { 1 }).ToArray());
        var second = await Service.ImportAsync(source); Assert.NotEqual(first.Path, second.Path);
        Assert.Equal(Png, await File.ReadAllBytesAsync(first.Path));
    }
    [Theory] [InlineData("file.txt")] [InlineData("file.svg")]
    public async Task UnsupportedFormatsAreRejected(string name)
    {
        var path = await Source(name);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(path));
    }
    [Fact] public async Task FakeImageExtensionIsRejectedWithoutTemporaryDebris()
    {
        var path = await Source(bytes: "not an image"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(path));
        Assert.Empty(Directory.EnumerateFiles(Library, "*", SearchOption.AllDirectories));
    }
    [Fact] public async Task EmptyAndOversizedImagesAreRejected()
    {
        var empty = await Source(bytes: Array.Empty<byte>());
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(empty));
        await using (var file = File.OpenWrite(empty)) file.SetLength(SkinPackageService.MaxAssetBytes + 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(empty));
    }
    [Fact] public async Task CancellationDoesNotCreateManagedFiles()
    {
        var path = await Source(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.ImportAsync(path, cancellation.Token));
        Assert.False(Directory.Exists(Library));
    }
    [Fact] public async Task MissingReferencesAreVisibleAndNotUsable()
    {
        var missing = Path.Combine(_root, "missing.png");
        var inventory = await Service.ListAsync(new[] { missing });
        var item = Assert.Single(inventory.Items); Assert.False(item.Available); Assert.Contains("Missing", item.Status);
    }
    [Fact] public async Task InventoryIncludesPackageImagesAndDeduplicatesReferences()
    {
        var package = Path.Combine(Library, "package-id"); Directory.CreateDirectory(package);
        var image = Path.Combine(package, "001.png"); await File.WriteAllBytesAsync(image, Png);
        var inventory = await Service.ListAsync(new[] { image, image });
        Assert.Single(inventory.Items); Assert.True(inventory.Items[0].Available);
    }
    [Fact] public async Task InventoryDoesNotCreateAnUnusedLibrary()
    {
        Assert.Empty((await Service.ListAsync()).Items); Assert.False(Directory.Exists(Library));
    }
    [Fact] public async Task InventoryIsBoundedAndReportsTruncation()
    {
        Directory.CreateDirectory(Library);
        for(int i = 0; i < WidgetAssetLibrary.MaxListedAssets + 1; i++) await File.WriteAllBytesAsync(Path.Combine(Library, $"{i}.png"), Png);
        var inventory = await Service.ListAsync(); Assert.Equal(WidgetAssetLibrary.MaxListedAssets, inventory.Items.Count); Assert.True(inventory.Truncated);
    }
    [Fact] public async Task AlteredManagedCopyIsNotSilentlyReused()
    {
        var source = await Source(); var asset = await Service.ImportAsync(source);
        await File.WriteAllTextAsync(asset.Path, "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(source));
    }
    [Fact] public async Task ExistingExternalImageCanBeBrowsedWithoutImporting()
    {
        var external = await Source(); var inventory = await Service.ListAsync(new[] { external });
        Assert.Equal(external, Assert.Single(inventory.Items).Path); Assert.False(Directory.Exists(Library));
    }
}
