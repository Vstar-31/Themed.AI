using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="ThemeRepository"/> and <see cref="SkinRepository"/>
/// covering load, save, seed defaults, import, export, and corruption recovery.
/// Uses isolated temp directories to avoid polluting real app data.
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly string _tempDir;

    public RepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThemedAI_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // ── ThemeRepository ──────────────────────────────────────────────────────

    [Fact]
    public async Task ThemeRepository_LoadAll_SeedsDefaultOnFirstRun()
    {
        var repo = new ThemeRepository(_tempDir);
        var themes = await repo.LoadAllAsync();

        Assert.NotNull(themes);
        Assert.NotEmpty(themes);

        var builtIn = themes.Find(t => t.Id == "cozy-default");
        Assert.NotNull(builtIn);
        Assert.True(builtIn!.IsBuiltIn);
    }

    [Fact]
    public async Task ThemeRepository_SaveAndLoad_RoundTrips()
    {
        var repo = new ThemeRepository(_tempDir);
        var themes = await repo.LoadAllAsync();

        var custom = new CozyTheme
        {
            Name = "Test Theme",
            BackgroundBase = "#AABBCC",
            AccentPrimary = "#112233",
        };
        themes.Add(custom);

        await repo.SaveAllAsync(themes);
        var reloaded = await repo.LoadAllAsync();

        Assert.Contains(reloaded, t => t.Name == "Test Theme");
        var found = reloaded.Find(t => t.Name == "Test Theme");
        Assert.NotNull(found);
        Assert.Equal("#AABBCC", found!.BackgroundBase);
    }

    [Fact]
    public async Task ThemeRepository_Export_CreatesValidJson()
    {
        var repo = new ThemeRepository(_tempDir);
        var theme = CozyDefaults.CreateDefault();
        var exportPath = Path.Combine(_tempDir, "exported.json");

        await repo.ExportThemeAsync(theme, exportPath);

        Assert.True(File.Exists(exportPath));
        string json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("Cozy", json);
    }

    [Fact]
    public async Task ThemeRepository_Import_GetsNewId()
    {
        var repo = new ThemeRepository(_tempDir);
        var theme = CozyDefaults.CreateDefault();
        var exportPath = Path.Combine(_tempDir, "import_test.json");

        await repo.ExportThemeAsync(theme, exportPath);

        var imported = await repo.ImportThemeAsync(exportPath);
        Assert.NotNull(imported);
        Assert.NotEqual("cozy-default", imported!.Id);
        Assert.False(imported.IsBuiltIn);
    }

    [Fact]
    public async Task ThemeRepository_ImportInvalidFile_ReturnsNull()
    {
        var repo = new ThemeRepository(_tempDir);
        var badPath = Path.Combine(_tempDir, "bad.json");
        await File.WriteAllTextAsync(badPath, "this is not json!!!");

        var imported = await repo.ImportThemeAsync(badPath);
        Assert.Null(imported);
    }

    [Fact]
    public async Task ThemeRepository_ImportNonexistentFile_ReturnsNull()
    {
        var repo = new ThemeRepository(_tempDir);
        var result = await repo.ImportThemeAsync(Path.Combine(_tempDir, "doesnotexist.json"));
        Assert.Null(result);
    }

    // ── SkinRepository ───────────────────────────────────────────────────────

    [Fact]
    public async Task SkinRepository_LoadAll_SeedsDefaultsOnFirstRun()
    {
        var repo = new SkinRepository(_tempDir);
        var skins = await repo.LoadAllAsync();

        Assert.NotNull(skins);
        Assert.NotEmpty(skins);
        Assert.Contains(skins, s => s.Name == "Cozy Clock");
        Assert.Contains(skins, s => s.Name == "System Monitor");
    }

    [Fact]
    public async Task SkinRepository_SaveAndLoad_RoundTrips()
    {
        var repo = new SkinRepository(_tempDir);
        var skins = await repo.LoadAllAsync();

        string uniqueName = "My Custom Widget " + Guid.NewGuid();
        var custom = new SkinDefinition
        {
            Name = uniqueName,
            Width = 300,
            Height = 200,
            Opacity = 0.75,
        };
        skins.Add(custom);

        await repo.SaveAllAsync(skins);
        var reloaded = await repo.LoadAllAsync();

        Assert.Contains(reloaded, s => s.Name == uniqueName);
        var found = reloaded.Find(s => s.Name == uniqueName);
        Assert.NotNull(found);
        Assert.Equal(0.75, found!.Opacity);
    }

    [Fact]
    public async Task ThemeRepository_StorageFoldersAreIsolated()
    {
        var firstFolder = Path.Combine(_tempDir, "first");
        var secondFolder = Path.Combine(_tempDir, "second");
        var custom = new CozyTheme { Name = "Isolated Theme" };
        await new ThemeRepository(firstFolder).SaveAllAsync(new[] { custom });

        var first = await new ThemeRepository(firstFolder).LoadAllAsync();
        var second = await new ThemeRepository(secondFolder).LoadAllAsync();

        Assert.Contains(first, theme => theme.Id == custom.Id);
        Assert.DoesNotContain(second, theme => theme.Id == custom.Id);
        Assert.True(File.Exists(Path.Combine(firstFolder, "themes.json")));
        Assert.True(File.Exists(Path.Combine(secondFolder, "themes.json")));
    }

    [Fact]
    public async Task SkinRepository_StorageFoldersAreIsolated()
    {
        var firstFolder = Path.Combine(_tempDir, "first");
        var secondFolder = Path.Combine(_tempDir, "second");
        var custom = new SkinDefinition { Name = "Isolated Widget" };
        await new SkinRepository(firstFolder).SaveAllCheckedAsync(new[] { custom });

        var first = await new SkinRepository(firstFolder).LoadAllAsync();
        var second = await new SkinRepository(secondFolder).LoadAllAsync();

        Assert.Contains(first, skin => skin.Id == custom.Id);
        Assert.DoesNotContain(second, skin => skin.Id == custom.Id);
        Assert.True(File.Exists(Path.Combine(firstFolder, "skins.json")));
        Assert.True(File.Exists(Path.Combine(secondFolder, "skins.json")));
    }
}
