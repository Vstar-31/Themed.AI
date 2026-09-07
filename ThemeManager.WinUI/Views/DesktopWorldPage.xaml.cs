using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.Views;

public sealed partial class DesktopWorldPage : Page
{
    public DesktopWorldPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            App.SceneService.ScenesChanged += OnScenesChanged;
            App.SceneService.ActiveSceneChanged += OnActiveSceneChanged;
            VibeSnapshotHub.SnapshotChanged += OnSnapshotChanged;
            RefreshScenes();
            UpdateAtmosphere(VibeSnapshotHub.Current);
        };
        Unloaded += (_, _) =>
        {
            App.SceneService.ScenesChanged -= OnScenesChanged;
            App.SceneService.ActiveSceneChanged -= OnActiveSceneChanged;
            VibeSnapshotHub.SnapshotChanged -= OnSnapshotChanged;
        };
    }

    private void OnScenesChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshScenes);
    private void OnActiveSceneChanged(object? sender, DesktopScene? scene) => DispatcherQueue.TryEnqueue(RefreshScenes);
    private void OnSnapshotChanged(object? sender, VibeSnapshot snapshot) => DispatcherQueue.TryEnqueue(() => UpdateAtmosphere(snapshot));

    private void UpdateAtmosphere(VibeSnapshot snapshot)
    {
        var atmosphere = App.WorldRuntime?.CurrentAtmosphere ?? DesktopVibeAdapter.From(snapshot.Mood, snapshot.Energy, snapshot.Warmth);
        MoodText.Text = atmosphere.Mood;
        EnergyText.Text = $"{atmosphere.Energy:P0}";
        WarmthText.Text = $"{atmosphere.Warmth:P0}";
        EnergyBar.Value = atmosphere.Energy;
        SourceText.Text = snapshot.Source == "VibeFinder" && snapshot.TrackTitle is not null
            ? $"Live from VibeFinder AI · {snapshot.TrackTitle} — {snapshot.TrackArtist}"
            : $"Signal source · {snapshot.Source}";
        RuntimeStatus.Text = App.WorldRuntime is null ? "WORLD STARTING" : "WORLD ONLINE";
    }

    private void RefreshScenes()
    {
        SceneList.Children.Clear();
        foreach (var scene in App.SceneService.Scenes.OrderByDescending(s => s.Id == App.SceneService.ActiveScene?.Id).ThenBy(s => s.Name))
        {
            var active = App.SceneService.ActiveScene?.Id.Equals(scene.Id, StringComparison.OrdinalIgnoreCase) == true;
            var grid = new Grid { ColumnSpacing = 14, Padding = new Thickness(18, 14, 12, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel { Spacing = 3 };
            info.Children.Add(new TextBlock { Text = scene.Name, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"] });
            info.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(scene.Description) ? $"{scene.Widgets.Count} widgets · {scene.Effects.Count} effects" : scene.Description, Style = (Style)Application.Current.Resources["CaptionStyle"], TextTrimming = TextTrimming.CharacterEllipsis });
            if (scene.Tags.Count > 0) info.Children.Add(new TextBlock { Text = string.Join("  ·  ", scene.Tags.Take(4)), Style = (Style)Application.Current.Resources["CaptionStyle"] });
            grid.Children.Add(info);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var activate = new Button { Content = active ? "Active" : "Activate", Tag = scene, IsEnabled = !active };
            activate.Click += Activate_Click;
            actions.Children.Add(activate);
            if (!scene.Id.StartsWith("builtin-", StringComparison.OrdinalIgnoreCase))
            {
                var delete = new Button { Content = "Delete", Tag = scene };
                delete.Click += Delete_Click;
                actions.Children.Add(delete);
            }
            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);
            var border = new Border { Child = grid, Style = (Style)Application.Current.Resources["CardStyle"] };
            SceneList.Children.Add(border);
        }
        if (SceneList.Children.Count == 0)
            SceneList.Children.Add(new TextBlock { Text = "No scenes yet. Pick a quick-build mood above to create your first one.", Style = (Style)Application.Current.Resources["CaptionStyle"] });
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DesktopScene scene }) App.SceneService.SetActiveScene(scene);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DesktopScene scene }) await App.SceneService.DeleteAsync(scene.Id);
    }

    private async void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        var scene = CreatePreset(tag);
        await App.SceneService.UpsertAsync(scene);
        App.SceneService.SetActiveScene(scene);
    }

    private async void NewScene_Click(object sender, RoutedEventArgs e)
    {
        var scene = new DesktopScene
        {
            Name = $"My World {App.SceneService.Scenes.Count + 1}",
            Description = "A personal desktop world, ready to shape.",
            ThemeId = App.ThemeService.ActiveTheme.Id,
            Tags = new List<string> { "personal" },
            Effects = new List<SceneEffect> { new() { Type = "Glow", Intensity = 0.35 }, new() { Type = "AmbientMotion", Intensity = 0.25 } }
        };
        await App.SceneService.UpsertAsync(scene);
        App.SceneService.SetActiveScene(scene);
    }

    private DesktopScene CreatePreset(string tag)
    {
        var (name, description, effects, motion, sensitivity) = tag.ToLowerInvariant() switch
        {
            "neon" => ("Neon Pulse", "Energetic glow, reactive motion and a little midnight chaos.", new[] { ("Glow", 0.8), ("Spectrum", 0.9), ("AmbientMotion", 0.7) }, 0.7, 0.9),
            "nocturnal" => ("Nocturnal", "Quiet, cinematic and tuned for late-night focus.", new[] { ("Glow", 0.45), ("AmbientMotion", 0.22) }, 0.22, 0.55),
            _ => ("Cozy Flow", "Warm, soft and quietly alive.", new[] { ("Glow", 0.5), ("AmbientMotion", 0.3) }, 0.3, 0.5)
        };
        return new DesktopScene
        {
            Id = $"builtin-{tag}", Name = name, Description = description, ThemeId = App.ThemeService.ActiveTheme.Id,
            Tags = new List<string> { tag, "starter" },
            Effects = effects.Select(e => new SceneEffect { Type = e.Item1, Intensity = e.Item2 }).ToList(),
            Behavior = new SceneBehavior { ReactToVibeFinder = true, ReactToMedia = true, AutoSwitch = true, MotionIntensity = motion, AudioSensitivity = sensitivity, TransitionSeconds = tag == "neon" ? 0.55 : 1.1 }
        };
    }
}
