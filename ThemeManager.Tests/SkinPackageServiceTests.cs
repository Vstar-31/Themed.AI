using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class SkinPackageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ThemedAI-packages", Guid.NewGuid().ToString("N"));
    private string Package => Path.Combine(_root, "widget.themedwidget");
    private string Assets => Path.Combine(_root, "installed");
    private SkinPackageService Service => new(Assets);
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public SkinPackageServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ExportImport_RoundTripsWidgetAndCreatesNewIdentity()
    {
        var original = new SkinDefinition
        {
            Id = "original-id", Name = "Clock", Description = "A portable clock widget",
            Tags = new() { "clock", "minimal" }, UpdateIntervalMs = 250, AlwaysOnTop = true,
        };
        original.Measures.Add(new MeasureDefinition { Name = "Time", Type = MeasureType.Time });
        original.Meters.Add(new MeterDefinition { MeasureName = "Time", Format = "{1}", Width = 120, Height = 24, ZIndex = 2 });
        original.Groups.Add(new WidgetGroupDefinition
        {
            Name = "Face", MeterIds = new() { original.Meters[0].Id }, Rotation = 12, ScaleX = 1.5,
            Variables = new() { ["caption"] = "Clock" },
        });
        await Service.ExportAsync(original, Package);
        var imported = await Service.ImportAsync(Package);

        Assert.Equal(original.Name, imported.Name);
        Assert.Equal(original.Description, imported.Description);
        Assert.Equal(original.Tags, imported.Tags);
        Assert.Equal(original.UpdateIntervalMs, imported.UpdateIntervalMs);
        Assert.NotEqual(original.Id, imported.Id);
        Assert.False(imported.Enabled);
        Assert.True(original.Enabled);
        Assert.True(imported.AlwaysOnTop);
        Assert.Equal(SkinDefinition.CurrentSchemaVersion, imported.SchemaVersion);
        Assert.Equal(2, imported.Meters.Single().ZIndex);
        var group = Assert.Single(imported.Groups);
        Assert.Equal(imported.Meters[0].Id, Assert.Single(group.MeterIds));
        Assert.Equal(12, group.Rotation);
        Assert.Equal(1.5, group.ScaleX);
        Assert.Equal("Clock", group.Variables["caption"]);
    }

    [Fact]
    public async Task Images_AreBundledOnceAndSurviveSourceDeletionAndReexport()
    {
        var source = Path.Combine(_root, "private-photo-name.png");
        await File.WriteAllBytesAsync(source, Png);
        var original = new SkinDefinition
        {
            Meters = new()
            {
                new() { Kind = MeterKind.Image, ImagePath = source },
                new() { Kind = MeterKind.Image, ImagePath = source },
            },
        };
        await Service.ExportAsync(original, Package);
        Assert.Equal(source, original.Meters[0].ImagePath);
        using (var archive = ZipFile.OpenRead(Package))
        {
            Assert.Equal(2, archive.Entries.Count);
            using var reader = new StreamReader(archive.GetEntry("skin.json")!.Open());
            var manifest = await reader.ReadToEndAsync();
            Assert.DoesNotContain("private-photo-name", manifest);
            Assert.Contains("assets/001.png", manifest);
        }
        File.Delete(source);
        var imported = await Service.ImportAsync(Package);
        Assert.Equal(imported.Meters[0].ImagePath, imported.Meters[1].ImagePath);
        Assert.StartsWith(Assets, imported.Meters[0].ImagePath);
        Assert.Equal(Png, await File.ReadAllBytesAsync(imported.Meters[0].ImagePath!));
        var another = await Service.ImportAsync(Package);
        Assert.NotEqual(imported.Meters[0].ImagePath, another.Meters[0].ImagePath);
        var reexport = Path.Combine(_root, "again.themedwidget");
        await Service.ExportAsync(imported, reexport);
        var roundTrip = await Service.ImportAsync(reexport);
        Assert.Equal(Png, await File.ReadAllBytesAsync(roundTrip.Meters[0].ImagePath!));
    }

    [Theory]
    [InlineData(MeasureType.WeatherTemp)]
    [InlineData(MeasureType.WeatherDesc)]
    [InlineData(MeasureType.WeatherCity)]
    [InlineData(MeasureType.VibeTrackTitle)]
    [InlineData(MeasureType.VibeTrackArtist)]
    [InlineData(MeasureType.VibeMood)]
    [InlineData(MeasureType.VibeTrackProgress)]
    [InlineData(MeasureType.VibePlaybackState)]
    [InlineData(MeasureType.WebJson)]
    public async Task Export_OmitsConnectionTargetsWithoutMutatingOriginal(MeasureType type)
    {
        var skin = new SkinDefinition { Measures = new() { new() { Name = "Data", Type = type, Target = "private-connection" } } };
        await Service.ExportAsync(skin, Package);
        var imported = await Service.ImportAsync(Package);
        Assert.Null(imported.Measures.Single().Target);
        Assert.Equal("private-connection", skin.Measures[0].Target);
    }

    [Fact]
    public async Task Export_OmitsPluginTargetsAndPreservesLocalMeasureTargets()
    {
        var skin = new SkinDefinition
        {
            Measures = new()
            {
                new() { Name = "Plugin", PluginType = "example", Target = "private-connection" },
                new() { Name = "Core", Type = MeasureType.CpuCore, Target = "2" },
            },
        };
        await Service.ExportAsync(skin, Package);
        var imported = await Service.ImportAsync(Package);
        Assert.Null(imported.Measures[0].Target);
        Assert.Equal("2", imported.Measures[1].Target);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("assets/../escape.png")]
    [InlineData("assets\\escape.png")]
    [InlineData("/absolute.png")]
    [InlineData("C:/absolute.png")]
    [InlineData("assets/file.png:stream")]
    [InlineData("assets/evil.dll")]
    [InlineData("assets/evil.svg")]
    [InlineData("assets/hidden.png.")]
    public async Task Import_RejectsUnsafeOrUnsupportedEntriesBeforeWriting(string entry)
    {
        CreatePackage(new SkinDefinition(), (entry, Png));
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.False(Directory.Exists(Assets));
    }

    [Fact]
    public async Task Import_RejectsCaseInsensitiveDuplicateEntries()
    {
        CreatePackage(new SkinDefinition(), ("assets/a.png", Png), ("assets/A.PNG", Png));
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.False(Directory.Exists(Assets));
    }

    [Fact]
    public async Task Import_RejectsDuplicateManifest()
    {
        CreatePackage(new SkinDefinition(), ("SKIN.JSON", JsonSerializer.SerializeToUtf8Bytes(new SkinDefinition(), JsonOptions)));
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
    }

    [Fact]
    public async Task Import_RejectsSymbolicLinks()
    {
        CreatePackage(new SkinDefinition(), ("assets/link.png", Png));
        using (var archive = ZipFile.Open(Package, ZipArchiveMode.Update))
            archive.GetEntry("assets/link.png")!.ExternalAttributes = unchecked((int)0xA1FF0000);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.False(Directory.Exists(Assets));
    }

    [Theory]
    [InlineData("assets/missing.png")]
    [InlineData("https://example.com/image.png")]
    [InlineData("C:\\Users\\other\\image.png")]
    [InlineData("/tmp/private.png")]
    public async Task Import_RejectsMissingOrExternalImageReferences(string reference)
    {
        CreatePackage(new SkinDefinition { Meters = new() { new() { Kind = MeterKind.Image, ImagePath = reference } } });
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.False(Directory.Exists(Assets));
    }

    [Fact]
    public async Task Import_RejectsExpandedAssetLimit()
    {
        CreatePackage(new SkinDefinition(), ("assets/large.png", new byte[SkinPackageService.MaxAssetBytes + 1]));
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.False(Directory.Exists(Assets));
    }

    [Fact]
    public async Task Import_RejectsEntryCountLimit()
    {
        CreatePackage(new SkinDefinition(), Enumerable.Range(0, SkinPackageService.MaxEntries)
            .Select(i => ($"assets/{i}.png", Png)).ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{invalid")]
    [InlineData("{\"Measures\":null}")]
    [InlineData("{\"Meters\":[null]}")]
    [InlineData("{\"Groups\":[null]}")]
    [InlineData("{\"Meters\":[{\"Kind\":999}]}")]
    [InlineData("{\"SchemaVersion\":999}")]
    public async Task Import_ReportsInvalidDefinitions(string json)
    {
        using (var archive = ZipFile.Open(Package, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("skin.json").Open());
            writer.Write(json);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.ImportAsync(Package));
        Assert.False(Directory.Exists(Assets));
    }

    [Fact]
    public async Task Import_MigratesLegacyVersionAndResetsOffscreenPlacement()
    {
        CreatePackage(new SkinDefinition { SchemaVersion = 2, AlwaysOnTop = true, X = -10000, Y = 10000 });
        var imported = await Service.ImportAsync(Package);
        Assert.Equal(SkinDefinition.CurrentSchemaVersion, imported.SchemaVersion);
        Assert.False(imported.AlwaysOnTop);
        Assert.False(imported.Enabled);
        Assert.Equal(40, imported.X);
        Assert.Equal(40, imported.Y);
    }

    [Fact]
    public async Task Export_MissingImageDoesNotReplaceExistingPackageOrLeaveTemporaryFiles()
    {
        await File.WriteAllTextAsync(Package, "keep me");
        var skin = new SkinDefinition
        {
            Meters = new() { new() { Kind = MeterKind.Image, ImagePath = Path.Combine(_root, "missing.png") } },
        };
        await Assert.ThrowsAsync<FileNotFoundException>(() => Service.ExportAsync(skin, Package));
        Assert.Equal("keep me", await File.ReadAllTextAsync(Package));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task Export_CancellationPreservesExistingPackage()
    {
        await File.WriteAllTextAsync(Package, "keep me");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.ExportAsync(new SkinDefinition(), Package, cancellation.Token));
        Assert.Equal("keep me", await File.ReadAllTextAsync(Package));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    private void CreatePackage(SkinDefinition skin, params (string Name, byte[] Content)[] assets)
    {
        using var archive = ZipFile.Open(Package, ZipArchiveMode.Create);
        using (var stream = archive.CreateEntry("skin.json").Open())
            JsonSerializer.Serialize(stream, skin, JsonOptions);
        foreach (var (name, content) in assets)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(content);
        }
    }
}
