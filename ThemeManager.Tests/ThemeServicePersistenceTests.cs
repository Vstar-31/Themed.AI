using System.Text.Json;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class ThemeServicePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "theme-service-" + Guid.NewGuid().ToString("N"));
    private string Library => Path.Combine(_root, "themes.json");
    private ThemeRepository Repository => new(_root);

    private async Task<ThemeService> CreateServiceAsync()
    {
        var service = new ThemeService(Repository);
        await service.InitializeAsync();
        return service;
    }

    // Windows permits reads but denies replacement of this open destination file.
    private FileStream BlockReplacement() => new(Library, FileMode.Open, FileAccess.Read, FileShare.Read);

    private static async Task AssertSaveFailsAsync(Func<Task> action)
    {
        var error = await Record.ExceptionAsync(action);
        Assert.True(error is IOException or UnauthorizedAccessException,
            $"Expected a file replacement failure, got {error?.GetType().Name ?? "no exception"}.");
    }
    [Theory]
    [InlineData("create")]
    [InlineData("duplicate")]
    [InlineData("import")]
    [InlineData("save-new")]
    public async Task FailedAdditionPreservesMembershipAndCanBeRetried(string operation)
    {
        var service = await CreateServiceAsync();
        var source = new CozyTheme { Name = "Pending theme" };
        var import = Path.Combine(_root, "import.json");
        await File.WriteAllTextAsync(import, JsonSerializer.Serialize(source));
        var before = service.Themes.ToArray();
        var saved = await File.ReadAllTextAsync(Library);
        var notifications = 0;
        service.ThemeListChanged += (_, _) => notifications++;

        async Task AddAsync()
        {
            switch (operation)
            {
                case "create": await service.CreateThemeAsync("Pending theme"); break;
                case "duplicate": await service.DuplicateThemeAsync(source); break;
                case "import": await service.ImportThemeAsync(import); break;
                default: await service.SaveThemeAsync(source); break;
            }
        }

        using (BlockReplacement())
            await AssertSaveFailsAsync(AddAsync);

        Assert.Equal(before, service.Themes);
        Assert.Equal(saved, await File.ReadAllTextAsync(Library));
        Assert.Equal(0, notifications);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        await AddAsync();
        Assert.Equal(before.Length + 1, service.Themes.Count);
        Assert.Equal(1, notifications);
        Assert.Equal(service.Themes.Count, (await Repository.LoadAllAsync()).Count);
    }

    [Fact]
    public async Task FailedActiveDeletionPreservesSelectionAndPublishesOnlyAfterSuccessfulRetry()
    {
        var service = await CreateServiceAsync();
        var active = await service.CreateThemeAsync("Keep me");
        service.SetActiveTheme(active);
        var listChanges = 0;
        var activeChanges = 0;
        service.ThemeListChanged += (_, _) => listChanges++;
        service.ThemeChanged += (_, _) => activeChanges++;
        var saved = await File.ReadAllTextAsync(Library);

        using (BlockReplacement())
            await AssertSaveFailsAsync(() => service.DeleteThemeAsync(active));

        Assert.Contains(active, service.Themes);
        Assert.Same(active, service.ActiveTheme);
        Assert.Equal(0, listChanges);
        Assert.Equal(0, activeChanges);
        Assert.Equal(saved, await File.ReadAllTextAsync(Library));
        await service.DeleteThemeAsync(active);
        Assert.DoesNotContain(active, service.Themes);
        Assert.NotEqual(active.Id, service.ActiveTheme.Id);
        Assert.Equal(1, listChanges);
        Assert.Equal(1, activeChanges);
        Assert.DoesNotContain(await Repository.LoadAllAsync(), t => t.Id == active.Id);
    }

    [Fact]
    public async Task DetachedSaveReplacesByIdentityAndUpdatesActiveInstanceAfterPersistence()
    {
        var service = await CreateServiceAsync();
        var original = await service.CreateThemeAsync("Original");
        service.SetActiveTheme(original);
        var replacement = new CozyTheme { Id = original.Id, Name = "Edited", AccentPrimary = "abc" };
        var timestamp = replacement.LastModified;
        var activeChanges = 0;
        service.ThemeChanged += (_, _) => activeChanges++;

        using (BlockReplacement())
            await AssertSaveFailsAsync(() => service.SaveThemeAsync(replacement));

        Assert.Same(original, Assert.Single(service.Themes, t => t.Id == original.Id));
        Assert.Same(original, service.ActiveTheme);
        Assert.Equal(timestamp, replacement.LastModified);
        Assert.Equal(0, activeChanges);
        await service.SaveThemeAsync(replacement);
        Assert.Same(replacement, Assert.Single(service.Themes, t => t.Id == original.Id));
        Assert.Same(replacement, service.ActiveTheme);
        Assert.Equal("#AABBCC", service.ActiveTheme.AccentPrimary);
        Assert.Equal(1, activeChanges);
        Assert.Equal("Edited", Assert.Single(await Repository.LoadAllAsync(), t => t.Id == original.Id).Name);
    }

    [Fact]
    public async Task ConcurrentAdditionsRetainEverySuccessfulThemeOnDisk()
    {
        var service = await CreateServiceAsync();
        var created = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => service.CreateThemeAsync("Theme " + i)));
        var stored = await Repository.LoadAllAsync();
        foreach (var theme in created)
        {
            Assert.Single(service.Themes, t => t.Id == theme.Id);
            Assert.Single(stored, t => t.Id == theme.Id);
        }
    }

    [Fact]
    public async Task DetachedDeleteResolvesStoredIdentityAndProtectsBuiltIns()
    {
        var service = await CreateServiceAsync();
        var custom = await service.CreateThemeAsync("Remove me");
        await service.DeleteThemeAsync(new CozyTheme { Id = custom.Id });
        Assert.DoesNotContain(service.Themes, t => t.Id == custom.Id);
        var builtIn = service.Themes.First(t => t.IsBuiltIn);
        await service.DeleteThemeAsync(new CozyTheme { Id = builtIn.Id, IsBuiltIn = false });
        Assert.Contains(builtIn, service.Themes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
