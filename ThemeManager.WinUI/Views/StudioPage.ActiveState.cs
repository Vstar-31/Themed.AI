using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private bool _activeStateUiInstalled;

    private async void StudioPage_ActiveStateLoaded(object sender, RoutedEventArgs e)
    {
        if (_activeStateUiInstalled) return;
        _activeStateUiInstalled = true;

        // Clicking a world previews it without applying the world immediately.
        ScenesList.SelectionMode = ListViewSelectionMode.None;
        ScenesList.IsItemClickEnabled = true;
        ScenesList.ItemClick += ScenesList_ItemClickForPreview;
        App.SceneService.ActiveSceneChanged += ActiveStateSceneChanged;
        UpdateSetActiveButtonState();

        // Studio has two Loaded paths: this XAML handler and the initialization handler in
        // StudioPage.xaml.cs. The latter loads persisted scenes asynchronously, so this handler
        // can legitimately run before ActiveScene is hydrated. Retry briefly on the UI dispatcher
        // rather than leaving a persisted active world visually stuck on Cozy Café until another
        // navigation/activation happens.
        _ = RehydrateActiveWorldAsync();
    }

    private async Task RehydrateActiveWorldAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (App.SceneService.ActiveScene is { } active)
            {
                try
                {
                    await ApplyWorldPaletteAsync(active);
                    if (DispatcherQueue.HasThreadAccess)
                        Refresh();
                    else
                        DispatcherQueue.TryEnqueue(Refresh);
                }
                catch
                {
                    // The normal initialization handler may still be loading the scene repository.
                    // The next retry will re-attempt the same idempotent hydration.
                }
                return;
            }

            await Task.Delay(50);
        }
    }

    private void StudioPage_ActiveStateUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_activeStateUiInstalled) return;
        ScenesList.ItemClick -= ScenesList_ItemClickForPreview;
        App.SceneService.ActiveSceneChanged -= ActiveStateSceneChanged;
        _activeStateUiInstalled = false;
    }

    private async void ActiveStateSceneChanged(object? sender, ThemeManager.Core.Models.DesktopScene? scene)
    {
        if (DispatcherQueue.HasThreadAccess) UpdateSetActiveButtonState();
        else DispatcherQueue.TryEnqueue(UpdateSetActiveButtonState);

        if (scene is not null)
            await ApplyWorldPaletteAsync(scene);
    }

    private async Task ApplyWorldPaletteAsync(DesktopScene scene)
    {
        var tag = scene.Tags
            .FirstOrDefault(t => t.Equals("nocturnal", StringComparison.OrdinalIgnoreCase)
                              || t.Equals("cozy", StringComparison.OrdinalIgnoreCase)
                              || t.Equals("hud", StringComparison.OrdinalIgnoreCase)
                              || t.Equals("neon", StringComparison.OrdinalIgnoreCase)
                              || t.Equals("minimal", StringComparison.OrdinalIgnoreCase));
        if (tag is null) return;

        var worldThemeId = $"world-{tag.ToLowerInvariant()}";
        var worldTheme = App.ThemeService.Themes
            .FirstOrDefault(t => t.Id.Equals(worldThemeId, StringComparison.OrdinalIgnoreCase));

        if (worldTheme is null)
        {
            var current = App.ThemeService.ActiveTheme;
            worldTheme = current.Duplicate();
            worldTheme.Id = worldThemeId;
            worldTheme.Name = $"{scene.Name} · World";
            worldTheme.Description = scene.Description;
            worldTheme.IsBuiltIn = false;

            var palette = tag.ToLowerInvariant() switch
            {
                "nocturnal" => ("#101521", "#182235", "#263A57", "#6EA8FE", "#D9E7FF", "#EEF5FF", "#9EB4D4", "#344766", 0.92, 0.90),
                "cozy"      => ("#F5F1EA", "#D7C9B8", "#B2967D", "#7D5A44", "#4A342A", "#3B2A20", "#7F7065", "#E0D5C7", 1.00, 1.05),
                "hud"       => ("#071014", "#0D1B22", "#11303A", "#62E6F7", "#B8F7FF", "#E9FDFF", "#8FB7BE", "#214751", 0.92, 0.94),
                "neon"      => ("#0B0616", "#160D27", "#281343", "#FF4FD8", "#77F7FF", "#F7EFFF", "#B7A6C9", "#43265D", 1.05, 0.96),
                "minimal"   => ("#F4F6F8", "#E4E8EC", "#D2D9E0", "#5D7186", "#263646", "#24313D", "#71808E", "#CDD4DB", 0.92, 0.92),
                _           => default
            };

            worldTheme.BackgroundBase = palette.Item1;
            worldTheme.BackgroundAlt = palette.Item2;
            worldTheme.Surface = palette.Item3;
            worldTheme.AccentPrimary = palette.Item4;
            worldTheme.AccentStrong = palette.Item5;
            worldTheme.TextPrimary = palette.Item6;
            worldTheme.TextMuted = palette.Item7;
            worldTheme.BorderSubtle = palette.Item8;
            worldTheme.CornerRadiusScale = palette.Item9;
            worldTheme.DensityScale = palette.Item10;

            await App.ThemeService.SaveThemeAsync(worldTheme);
        }

        if (!scene.ThemeId.Equals(worldTheme.Id, StringComparison.OrdinalIgnoreCase))
        {
            scene.ThemeId = worldTheme.Id;
            await App.SceneService.UpsertAsync(scene);
        }

        App.ThemeService.SetActiveTheme(worldTheme);
        await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(worldTheme.AccentPrimary));
    }

    private void ScenesList_ItemClickForPreview(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WorldListItem item)
        {
            Select(item.Scene, false);
            UpdateSetActiveButtonState();
        }
    }

    private void UpdateSetActiveButtonState()
    {
        if (SetActiveWorldButton is null) return;

        var active = _selected is not null && ReferenceEquals(_selected, App.SceneService.ActiveScene);
        SetActiveWorldButton.Content = active ? "Active world ✓" : "Activate this world";
        SetActiveWorldButton.IsEnabled = _selected is not null && !active;
    }

    private async void SetSelectedWorldActive_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ApplyStatus.Text = "Select a world first.";
            return;
        }

        // Activation is a real desktop operation: apply the world's saved theme, wallpaper,
        // widget placements and visibility, then persist it as the active world.
        await ApplySelectedWorldAsync();

        // Vibe-tagged worlds participate in the feedback-aware desktop loop by default.
        if (_selected.Tags.Any(t => t.Equals("vibe", StringComparison.OrdinalIgnoreCase)) &&
            !_selected.Behavior.AutoSwitch)
        {
            _selected.Behavior.AutoSwitch = true;
            await App.SceneService.UpsertAsync(_selected);
            ApplyStatus.Text = "Applied to desktop ✓ · Vibe auto-switch on";
        }

        UpdateSetActiveButtonState();
    }
}
