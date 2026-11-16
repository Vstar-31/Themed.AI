using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.Integration;
using ThemeManager.WinUI;
using ThemeManager.WinUI.Services;

namespace ThemeManager.Tests;

public sealed class WorldAppearanceTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("themed-world-").FullName;
    private readonly DesktopSceneRepository _repository;
    private readonly RecordingWindows _windows = new();

    public WorldAppearanceTests()
    {
        _repository = new DesktopSceneRepository(_folder);
        App.SceneService = new DesktopSceneService(_repository);
        App.ThemeRepository = new ThemeRepository(_folder);
        App.ThemeService = new ThemeService(App.ThemeRepository, new ThemeSyncModule(_windows).ApplyAsync);
        App.SystemIntegrator = _windows;
        App.SkinManager = new SkinManagerService();
        App.SkinManager.Skins.Add(new SkinDefinition { Id = "clock", Enabled = true });
        App.SkinManager.Skins.Add(new SkinDefinition { Id = "music", Enabled = false });
    }

    private async Task<DesktopScene> PrepareWorldAsync()
    {
        var previous = new DesktopScene { Id = "morning", Widgets = [new() { WidgetId = "clock" }] };
        var next = new DesktopScene
        {
            Id = "night",
            Widgets = [new() { WidgetId = "music" }],
            ThemeSnapshot = new CozyTheme { BackgroundBase = "#101010", AccentPrimary = "#ff8800",
                ApplyToWindowsApps = true, ApplyToSystemAccent = true, ApplyToWallpaper = true,
                WallpaperPath = Path.Combine(_folder, "theme.png") },
            WallpaperPath = Path.Combine(_folder, "world.png")
        };
        File.WriteAllBytes(next.WallpaperPath, []);
        File.WriteAllBytes(next.ThemeSnapshot.WallpaperPath, []);
        await App.SceneService.UpsertAsync(previous);
        await App.SceneService.UpsertAsync(next);
        await App.SceneService.SetAppliedSceneAsync(previous);
        return next;
    }

    [Fact]
    public async Task FailedSavePreservesAppearanceAndWidgetsThenRetryAppliesOnce()
    {
        var next = await PrepareWorldAsync();
        var previousTheme = App.ThemeService.ActiveTheme;
        var obstruction = Directory.CreateDirectory(Path.Combine(_folder, "active-scene.json.tmp"));
        var error = await Record.ExceptionAsync(() => WorldActivation.ApplyAsync(next));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("morning", await _repository.LoadActiveSceneIdAsync());
        Assert.Same(previousTheme, App.ThemeService.ActiveTheme);
        Assert.Empty(_windows.Writes);
        Assert.Equal("clock", Assert.Single(App.SkinManager.Skins, s => s.Enabled).Id);

        obstruction.Delete();
        var result = await WorldActivation.ApplyAsync(next);
        Assert.Equal(next.Id, await _repository.LoadActiveSceneIdAsync());
        Assert.Equal("music", Assert.Single(App.SkinManager.Skins, s => s.Enabled).Id);
        Assert.Same(next.ThemeSnapshot, App.ThemeService.ActiveTheme);
        Assert.Equal(new[] { "dark", "#FF8800", next.WallpaperPath }, _windows.Writes);
        Assert.Contains("world.png", result.AppearanceStatus);
    }

    [Fact]
    public async Task WindowsRejectionKeepsCommittedWorldAndReportsBothProblems()
    {
        var next = await PrepareWorldAsync();
        _windows.Accept = false;
        var result = await WorldActivation.ApplyAsync(next);
        Assert.Equal(next.Id, await _repository.LoadActiveSceneIdAsync());
        Assert.Same(next, App.SceneService.ActiveScene);
        Assert.Equal("music", Assert.Single(App.SkinManager.Skins, s => s.Enabled).Id);
        Assert.Contains("theme settings", result.AppearanceStatus);
        Assert.Contains("rejected the wallpaper", result.AppearanceStatus);
    }

    [Fact]
    public async Task AppearanceExceptionAfterSaveKeepsLayoutAndPublishesActiveWorld()
    {
        var next = await PrepareWorldAsync();
        string? published = null;
        App.SceneService.ActiveSceneChanged += (_, scene) => published = scene?.Id;
        App.ThemeService.ThemeChanged += (_, _) => throw new InvalidOperationException("test listener");
        var result = await WorldActivation.ApplyAsync(next);
        Assert.Equal(next.Id, await _repository.LoadActiveSceneIdAsync());
        Assert.Equal(next.Id, published);
        Assert.Equal("music", Assert.Single(App.SkinManager.Skins, s => s.Enabled).Id);
        Assert.Contains("could not finish", result.AppearanceStatus);
    }

    [Theory]
    [InlineData(true, false, "light")]
    [InlineData(false, true, "#FF8800")]
    public async Task WindowsPreferencesAreIndependent(bool mode, bool accent, string expected)
    {
        var next = await PrepareWorldAsync();
        var theme = next.ThemeSnapshot!;
        theme.BackgroundBase = "#fff";
        theme.ApplyToWindowsApps = mode;
        theme.ApplyToSystemAccent = accent;
        theme.ApplyToWallpaper = false;
        next.WallpaperPath = null;
        await WorldActivation.ApplyAsync(next);
        Assert.Equal(expected, Assert.Single(_windows.Writes));
    }

    [Fact]
    public async Task LinkedThemeHonorsOptOutsAndFallsBackToThemeWallpaper()
    {
        var next = await PrepareWorldAsync();
        var linked = next.ThemeSnapshot!;
        linked.Id = "linked";
        linked.ApplyToWindowsApps = linked.ApplyToSystemAccent = false;
        await App.ThemeService.SaveThemeAsync(linked);
        next.ThemeSnapshot = null;
        next.ThemeId = linked.Id;
        File.Delete(next.WallpaperPath!);
        await WorldActivation.ApplyAsync(next);
        Assert.Equal(linked.Id, App.ThemeService.ActiveTheme.Id);
        Assert.Equal(linked.WallpaperPath, Assert.Single(_windows.Writes));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class RecordingWindows : ISystemThemeIntegrator
    {
        public List<string> Writes { get; } = [];
        public bool Accept { get; set; } = true;
        private Task<bool> Write(string value) { Writes.Add(value); return Task.FromResult(Accept); }
        public Task<bool> ApplyWindowsThemeAsync(bool light) => Write(light ? "light" : "dark");
        public Task<bool> ApplyAccentColorAsync(string hex) => Write(hex);
        public Task<bool> ApplyWallpaperAsync(string path) => Write(path);
        public Task<string> GetCurrentAccentColorAsync() => throw new NotSupportedException();
        public Task<SystemThemeInfo> GetCurrentSystemThemeAsync() => throw new NotSupportedException();
        public Task<bool> ResetAccentColorAsync() => throw new NotSupportedException();
        public Task<bool> IsBatterySaverActiveAsync() => throw new NotSupportedException();
    }
}
