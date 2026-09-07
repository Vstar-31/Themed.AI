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
            Refresh();
        };
        App.SceneService.ScenesChanged += OnScenesChanged;
    }

    private void OnScenesChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        WorldList.ItemsSource = App.SceneService.Scenes.ToList();
        var scene = App.SceneService.ActiveScene ?? App.SceneService.Scenes.FirstOrDefault();
        if (scene is not null) Select(scene);
    }

    private void Select(DesktopScene scene)
    {
        _selected = scene;
        App.SceneService.SetActiveScene(scene);
        HeroTitle.Text = scene.Name;
        HeroDescription.Text = scene.Description;
        HeroMood.Text = scene.Tags.FirstOrDefault() ?? "aesthetic";
        HeroWidgets.Text = $"{scene.Widgets.Count} widgets";
        HeroAdaptive.Text = scene.Behavior.ReactToVibeFinder ? "Vibe adaptive" : "Static world";
    }

    private async void GenerateWorld_Click(object sender, RoutedEventArgs e) => await GenerateWorldAsync();

    private async Task GenerateWorldAsync()
    {
        var pick = Worlds[Random.Shared.Next(Worlds.Length)];
        var scene = new DesktopScene
        {
            Name = pick.Name,
            Description = pick.Description,
            ThemeId = App.ThemeService.ActiveTheme.Id,
            Tags = new List<string> { pick.Tag, "vibe" },
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

    private async void SurpriseMe_Click(object sender, RoutedEventArgs e)
    {
        await GenerateWorldAsync();
        await ApplySelectedThemeAsync();
    }

    private void World_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DesktopScene scene }) Select(scene);
    }

    private async Task ApplySelectedThemeAsync()
    {
        if (_selected is null) return;
        var target = (await App.ThemeRepository.LoadAllAsync()).FirstOrDefault(t => t.Id.Equals(_selected.ThemeId, StringComparison.OrdinalIgnoreCase));
        if (target is not null) App.ThemeService.SetActiveTheme(target);
    }
}
