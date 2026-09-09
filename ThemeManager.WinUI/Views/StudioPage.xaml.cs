using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage : Page
{
    private DesktopScene? _selected;
    private string? _lastWallpaperPath;
    private bool _wallpaperBusy;
    private bool _sceneApplyBusy;

    private static readonly (string Name, string Description, string Tag)[] Worlds =
    {
        ("Midnight Kyoto", "Rainy neon, quiet motion and a late-night city glow.", "nocturnal"),
        ("Rainy Café", "Warm paper tones and a calm little corner for deep focus.", "cozy"),
        ("Astral Terminal", "A dark systems cockpit with HUD energy and subtle motion.", "hud"),
        ("Neon Afterglow", "Electric color and music-reactive atmosphere for the night.", "neon"),
        ("Arctic Glass", "Cool light, clean glass and almost-silent movement.", "minimal")
    };

    public IReadOnlyList<string> WallpaperStyles { get; } = ThemedWallpaperGenerator.AvailableStyles;

    public IReadOnlyList<string> WallpaperSizes { get; } =
    [
        "1920 × 1080",
        "2560 × 1440",
        "2560 × 1080",
        "3440 × 1440",
        "3840 × 2160",
        "3840 × 1600"
    ];

    public StudioPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await App.SceneService.InitializeAsync();
            await App.StartWorldRuntimeAsync();
            await SeedStarterWorldsAsync();
            Refresh();
        };
        App.SceneService.ScenesChanged += OnScenesChanged;
        App.ThemeService.ThemeChanged += ThemeService_ThemeChanged;
        Unloaded += (_, _) =>
        {
            App.SceneService.ScenesChanged -= OnScenesChanged;
            App.ThemeService.ThemeChanged -= ThemeService_ThemeChanged;
        };
    }

    private void ThemeService_ThemeChanged(object? sender, CozyTheme e) =>
        DispatcherQueue.TryEnqueue(UpdateWallpaperStatus);

    private void OnScenesChanged(object? sender, EventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess) DispatcherQueue.TryEnqueue(Refresh); else Refresh();
    }

    private void Refresh()
    {
        ScenesList.ItemsSource = App.SceneService.Scenes.ToList();
        var scene = App.SceneService.ActiveScene ?? App.SceneService.Scenes.FirstOrDefault();
        if (scene is not null) Select(scene, false);
    }

    private void Select(DesktopScene scene, bool activate = true)
    {
        _selected = scene;
        if (activate) App.SceneService.SetActiveScene(scene);
        ActiveSceneName.Text = scene.Name;
        var vibe = VibeSnapshotHub.Current;
        var track = string.IsNullOrWhiteSpace(vibe.TrackTitle) ? "No track" : $"{vibe.TrackTitle} · {vibe.Artist}";
        ActiveSceneMeta.Text = $"{scene.Description} · {scene.Widgets.Count} widgets · {scene.Effects.Count} effects · {vibe.Mood} · {track}";
        ApplyStatus.Text = scene.Behavior.ReactToVibeFinder ? "Vibe adaptive · Ready to apply" : "Static world · Ready to apply";
        SceneDetails.Children.Clear();
        AddDetail("Mood", scene.Tags.FirstOrDefault() ?? "aesthetic");
        AddDetail("Widgets", scene.Widgets.Count.ToString());
        AddDetail("Effects", scene.Effects.Count.ToString());
        AddDetail("Adaptation", scene.Behavior.ReactToVibeFinder ? "VibeFinder + media" : "Static");
        AddDetail("Transition", $"{scene.Behavior.TransitionSeconds:0.0}s");
        UpdateWallpaperStatus();
    }

    private void AddDetail(string label, string value)
    {
        SceneDetails.Children.Add(new TextBlock { Text = $"{label}  ·  {value}", FontSize = 13, Opacity = 0.72 });
    }

    private void UpdateWallpaperStatus()
    {
        if (_selected is null) return;
        var wallpaper = _selected.WallpaperPath;
        if (!string.IsNullOrWhiteSpace(wallpaper) && File.Exists(wallpaper))
            WallpaperStatusText.Text = $"Bound to this world ✓  {Path.GetFileName(wallpaper)}";
        else
            WallpaperStatusText.Text = $"No wallpaper bound. Active theme: {App.ThemeService.ActiveTheme.Name} · {App.ThemeService.ActiveTheme.AccentPrimary} · generation is fully local/free.";
    }

    private async Task SeedStarterWorldsAsync()
    {
        if (App.SceneService.Scenes.Count > 0) return;

        var widgets = CaptureCurrentWidgetLayout();
        for (var i = 0; i < Worlds.Length; i++)
        {
            var pick = Worlds[i];
            var scene = new DesktopScene
            {
                Name = pick.Name,
                Description = pick.Description,
                ThemeId = App.ThemeService.ActiveTheme.Id,
                Tags = new List<string> { pick.Tag, "starter", "vibe" },
                Widgets = widgets.Select(w => new SceneWidgetPlacement
                {
                    WidgetId = w.WidgetId,
                    X = w.X,
                    Y = w.Y,
                    Scale = w.Scale,
                    Rotation = w.Rotation,
                    Opacity = w.Opacity,
                    ZIndex = w.ZIndex,
                    Visible = w.Visible,
                    Monitor = w.Monitor
                }).ToList(),
                Effects = new List<SceneEffect>
                {
                    new() { Type = pick.Tag == "minimal" ? "Glass" : "Glow", Intensity = pick.Tag is "neon" or "hud" ? 0.8 : 0.35 },
                    new() { Type = "AmbientMotion", Intensity = 0.25, Speed = 0.65 }
                },
                Behavior = new SceneBehavior { ReactToVibeFinder = true, ReactToMedia = true, TransitionSeconds = 0.9 }
            };
            await App.SceneService.UpsertAsync(scene);
        }
    }

    private async void GenerateWorld_Click(object sender, RoutedEventArgs e) => await CreateWorldAsync();

    private async Task CreateWorldAsync()
    {
        var pick = Worlds[Random.Shared.Next(Worlds.Length)];
        var scene = new DesktopScene
        {
            Name = pick.Name,
            Description = pick.Description,
            ThemeId = App.ThemeService.ActiveTheme.Id,
            Tags = new List<string> { pick.Tag, "generated", "vibe" },
            Widgets = CaptureCurrentWidgetLayout(),
            Effects = new List<SceneEffect>
            {
                new() { Type = pick.Tag == "minimal" ? "Glass" : "Glow", Intensity = pick.Tag is "neon" or "hud" ? 0.8 : 0.35 },
                new() { Type = "AmbientMotion", Intensity = 0.25, Speed = 0.65 }
            },
            Behavior = new SceneBehavior { ReactToVibeFinder = true, ReactToMedia = true, TransitionSeconds = 0.9 }
        };
        await App.SceneService.UpsertAsync(scene);
        Select(scene);
    }

    private List<SceneWidgetPlacement> CaptureCurrentWidgetLayout() =>
        App.SkinManager?.Skins
            .Where(w => w.Enabled)
            .Select((w, index) => new SceneWidgetPlacement
            {
                WidgetId = w.Id,
                X = w.X,
                Y = w.Y,
                Scale = 1.0,
                Rotation = 0,
                Opacity = Math.Clamp(w.Opacity, 0, 1),
                ZIndex = index,
                Visible = w.Enabled,
                Monitor = "Primary"
            })
            .ToList() ?? new List<SceneWidgetPlacement>();

    private async void SurpriseMe_Click(object sender, RoutedEventArgs e)
    {
        await CreateWorldAsync();
        await ApplySelectedWorldAsync();
    }

    private async void ApplyScene_Click(object sender, RoutedEventArgs e) => await ApplySelectedWorldAsync();

    private void ScenesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScenesList.SelectedItem is DesktopScene scene) Select(scene);
    }

    private async Task GenerateWallpaperAsync(bool applyAfterGeneration)
    {
        if (_selected is null)
        {
            WallpaperStatusText.Text = "Select a world first.";
            return;
        }

        if (_wallpaperBusy) return;
        _wallpaperBusy = true;

        try
        {
            var style = WallpaperStyleCombo.SelectedItem as string ?? WallpaperStyles[0];
            var (width, height) = ParseSize(WallpaperSizeCombo.SelectedItem as string);
            var name = string.IsNullOrWhiteSpace(WallpaperNameBox.Text)
                ? _selected.Name
                : WallpaperNameBox.Text.Trim();

            WallpaperStatusText.Text = $"Generating {style} wallpaper from {App.ThemeService.ActiveTheme.Name}…";
            var path = await ThemedWallpaperGenerator.GenerateAsync(
                App.ThemeService.ActiveTheme,
                style,
                width,
                height,
                name);

            _lastWallpaperPath = path;
            _selected.WallpaperPath = path;
            _selected.WallpaperFit = "Fill";
            _selected.WallpaperOpacity = 1.0;
            await App.SceneService.UpsertAsync(_selected);

            if (applyAfterGeneration)
            {
                WallpaperStatusText.Text = "Generated ✓ · applying to Windows…";
                var applied = await App.SystemIntegrator.ApplyWallpaperAsync(path);
                WallpaperStatusText.Text = applied
                    ? $"Applied ✓  {Path.GetFileName(path)} · bound to {_selected.Name}"
                    : $"Generated ✓ but Windows did not accept the wallpaper change. Saved at {path}";
            }
            else
            {
                WallpaperStatusText.Text = $"Generated ✓  {Path.GetFileName(path)} · bound to {_selected.Name}";
            }
        }
        catch (OperationCanceledException)
        {
            WallpaperStatusText.Text = "Wallpaper generation cancelled.";
        }
        catch (Exception ex)
        {
            WallpaperStatusText.Text = $"Wallpaper generation failed: {ex.Message}";
        }
        finally
        {
            _wallpaperBusy = false;
        }
    }

    private async void GenerateWallpaper_Click(object sender, RoutedEventArgs e) =>
        await GenerateWallpaperAsync(false);

    private async void GenerateAndApplyWallpaper_Click(object sender, RoutedEventArgs e) =>
        await GenerateWallpaperAsync(true);

    private async void ApplyLastWallpaper_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastWallpaperPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            path = _selected?.WallpaperPath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            WallpaperStatusText.Text = "No generated wallpaper is available for this world yet.";
            return;
        }

        try
        {
            var applied = await App.SystemIntegrator.ApplyWallpaperAsync(path);
            WallpaperStatusText.Text = applied
                ? $"Applied ✓  {Path.GetFileName(path)}"
                : "Windows rejected the wallpaper change.";
        }
        catch (Exception ex)
        {
            WallpaperStatusText.Text = $"Could not apply wallpaper: {ex.Message}";
        }
    }

    private static (int Width, int Height) ParseSize(string? value)
    {
        var numbers = value?
            .Replace("×", "x", StringComparison.Ordinal)
            .Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (numbers is { Length: 2 } &&
            int.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) &&
            int.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return (width, height);
        }

        return (1920, 1080);
    }

    private async Task ApplySelectedWorldAsync()
    {
        if (_selected is null || _sceneApplyBusy) return;
        _sceneApplyBusy = true;

        try
        {
            ApplyStatus.Text = $"Applying {_selected.Name}…";

            var themes = await App.ThemeRepository.LoadAllAsync();
            var targetTheme = themes.FirstOrDefault(t => t.Id.Equals(_selected.ThemeId, StringComparison.OrdinalIgnoreCase));

            if (targetTheme is not null)
            {
                App.ThemeService.SetActiveTheme(targetTheme);
                await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(targetTheme.AccentPrimary));
            }

            if (!string.IsNullOrWhiteSpace(_selected.WallpaperPath) && File.Exists(_selected.WallpaperPath))
            {
                var applied = await App.SystemIntegrator.ApplyWallpaperAsync(_selected.WallpaperPath);
                WallpaperStatusText.Text = applied
                    ? $"Applied ✓  {Path.GetFileName(_selected.WallpaperPath)} · bound to {_selected.Name}"
                    : $"World wallpaper saved, but Windows rejected the change: {Path.GetFileName(_selected.WallpaperPath)}";
            }
            else if (targetTheme is not null && targetTheme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(targetTheme.WallpaperPath) && File.Exists(targetTheme.WallpaperPath))
            {
                var applied = await App.SystemIntegrator.ApplyWallpaperAsync(targetTheme.WallpaperPath);
                WallpaperStatusText.Text = applied
                    ? $"Applied theme wallpaper ✓  {Path.GetFileName(targetTheme.WallpaperPath)}"
                    : "Theme wallpaper was not accepted by Windows.";
            }

            if (App.SkinManager is not null)
            {
                var known = App.SkinManager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
                var sceneIds = _selected.Widgets.Select(w => w.WidgetId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Apply all placement mutations without closing/reopening an already-visible widget.
                // The runtime owns the native window and receives one deterministic placement update.
                foreach (var placement in _selected.Widgets)
                {
                    if (!known.TryGetValue(placement.WidgetId, out var skin)) continue;
                    await App.SkinManager.ApplyScenePlacementAsync(
                        skin,
                        placement.X,
                        placement.Y,
                        placement.Opacity,
                        placement.Visible);
                }

                // A scene is a complete desktop snapshot: widgets absent from the selected world
                // should not linger from a different world. Their own persisted X/Y and z-order are
                // untouched, so switching back to another scene restores them exactly where they were.
                foreach (var skin in App.SkinManager.Skins.Where(s => s.Enabled && !sceneIds.Contains(s.Id)).ToList())
                    await App.SkinManager.ApplyScenePlacementAsync(skin, skin.X, skin.Y, skin.Opacity, false);
            }

            App.SceneService.SetActiveScene(_selected);
            ApplyStatus.Text = "Applied to desktop ✓";
            ActiveSceneMeta.Text = $"Applied · {_selected.Widgets.Count} saved widget placements · VibeFinder adaptation {(_selected.Behavior.ReactToVibeFinder ? "on" : "off")}";
        }
        finally
        {
            _sceneApplyBusy = false;
        }
    }
}
