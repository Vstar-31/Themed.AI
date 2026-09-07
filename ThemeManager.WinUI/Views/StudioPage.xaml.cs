using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage : Page
{
    private DesktopScene? _selected;
    private static readonly (string Name, string Description, string Tag)[] Worlds =
    {
        ("Midnight Kyoto", "Rainy neon, quiet motion and a late-night city glow.", "nocturnal"),
        ("Rainy Café", "Warm paper tones and a calm little corner for deep focus.", "cozy"),
        ("Astral Terminal", "A dark systems cockpit with HUD energy and subtle motion.", "hud"),
        ("Neon Afterglow", "Electric color and music-reactive atmosphere for the night.", "neon"),
        ("Arctic Glass", "Cool light, clean glass and almost-silent movement.", "minimal")
    };

    public StudioPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await App.SceneService.InitializeAsync();
            await SeedStarterWorldsAsync();
            Refresh();
        };
        App.SceneService.ScenesChanged += OnScenesChanged;
    }

    private void OnScenesChanged(object? sender, EventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess) DispatcherQueue.TryEnqueue(Refresh); else Refresh();
    }

    private void Refresh()
    {
        WorldList.ItemsSource = App.SceneService.Scenes.ToList();
        var scene = App.SceneService.ActiveScene ?? App.SceneService.Scenes.FirstOrDefault();
        if (scene is not null) Select(scene, false);
    }

    private void Select(DesktopScene scene, bool activate = true)
    {
        _selected = scene;
        if (activate) App.SceneService.SetActiveScene(scene);
        HeroTitle.Text = scene.Name;
        HeroDescription.Text = scene.Description;
        HeroMood.Text = scene.Tags.FirstOrDefault() ?? "aesthetic";
        HeroWidgets.Text = $"{scene.Widgets.Count} widgets";
        HeroAdaptive.Text = scene.Behavior.ReactToVibeFinder ? "Vibe adaptive" : "Static world";
    }

    private async Task SeedStarterWorldsAsync()
    {
        if (App.SceneService.Scenes.Count > 0) return;

        var widgets = App.SkinManager?.Skins.Where(s => s.Enabled).ToList() ?? new List<SkinDefinition>();
        for (var i = 0; i < Worlds.Length; i++)
        {
            var pick = Worlds[i];
            var scene = new DesktopScene
            {
                Name = pick.Name,
                Description = pick.Description,
                ThemeId = App.ThemeService.ActiveTheme.Id,
                Tags = new List<string> { pick.Tag, "starter", "vibe" },
                Widgets = widgets.Select((w, index) => new SceneWidgetPlacement
                {
                    WidgetId = w.Id,
                    X = 40 + ((index + i) % 3) * 240,
                    Y = 80 + ((index + i) % 3) * 150,
                    Opacity = w.Opacity,
                    ZIndex = index,
                    Visible = true
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
            Widgets = BuildCurrentWidgetLayout(),
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

    private List<SceneWidgetPlacement> BuildCurrentWidgetLayout()
    {
        return (App.SkinManager?.Skins ?? Array.Empty<SkinDefinition>()).Where(s => s.Enabled).Select((w, index) => new SceneWidgetPlacement
        {
            WidgetId = w.Id,
            X = 40 + (index % 4) * 220,
            Y = 80 + (index / 4) * 140,
            Opacity = w.Opacity,
            ZIndex = index,
            Visible = true
        }).ToList();
    }

    private async void SurpriseMe_Click(object sender, RoutedEventArgs e)
    {
        await CreateWorldAsync();
        await ApplySelectedWorldAsync();
    }

    private void World_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DesktopScene scene }) Select(scene);
    }

    private async void ApplyScene_Click(object sender, RoutedEventArgs e) => await ApplySelectedWorldAsync();

    private async Task ApplySelectedWorldAsync()
    {
        if (_selected is null) return;

        var themes = await App.ThemeRepository.LoadAllAsync();
        var targetTheme = themes.FirstOrDefault(t => t.Id.Equals(_selected.ThemeId, StringComparison.OrdinalIgnoreCase));
        if (targetTheme is not null) App.ThemeService.SetActiveTheme(targetTheme);

        if (!string.IsNullOrWhiteSpace(_selected.WallpaperPath) && File.Exists(_selected.WallpaperPath))
            await App.SystemIntegrator.ApplyWallpaperAsync(_selected.WallpaperPath);

        if (App.SkinManager is not null)
        {
            var known = App.SkinManager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var placement in _selected.Widgets)
            {
                if (!known.TryGetValue(placement.WidgetId, out var skin)) continue;
                skin.X = placement.X;
                skin.Y = placement.Y;
                skin.Opacity = Math.Clamp(placement.Opacity, 0, 1);
                skin.ZIndex = placement.ZIndex;
                await App.SkinManager.SetOpacityAsync(skin, skin.Opacity);
                await App.SkinManager.SaveSkinAsync(skin);
                await App.SkinManager.SetEnabledAsync(skin, placement.Visible);
            }
        }

        App.SceneService.SetActiveScene(_selected);
        ActiveSceneName.Text = _selected.Name;
        ActiveSceneMeta.Text = $"Applied · {_selected.Widgets.Count} widget placements · VibeFinder adaptation {( _selected.Behavior.ReactToVibeFinder ? "on" : "off" )}";
    }
}
