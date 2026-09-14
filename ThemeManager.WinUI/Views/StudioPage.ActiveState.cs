using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private bool _activeStateUiInstalled;

    private void StudioPage_ActiveStateLoaded(object sender, RoutedEventArgs e)
    {
        if (_activeStateUiInstalled) return;
        _activeStateUiInstalled = true;

        // Clicking a world previews it without applying the world immediately.
        ScenesList.SelectionMode = ListViewSelectionMode.None;
        ScenesList.IsItemClickEnabled = true;
        ScenesList.ItemClick += ScenesList_ItemClickForPreview;
        App.SceneService.ActiveSceneChanged += ActiveStateSceneChanged;
        UpdateSetActiveButtonState();
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

        var palette = tag.ToLowerInvariant() switch
        {
            "nocturnal" => ("#101521", "#182235", "#263A57", "#6EA8FE", "#D9E7FF", "#EEF5FF", "#9EB4D4", "#344766", 0.92, 0.90),
            "cozy"      => ("#F5F1EA", "#D7C9B8", "#B2967D", "#7D5A44", "#4A342A", "#3B2A20", "#7F7065", "#E0D5C7", 1.00, 1.05),
            "hud"       => ("#071014", "#0D1B22", "#11303A", "#62E6F7", "#B8F7FF", "#E9FDFF", "#8FB7BE", "#214751", 0.92, 0.94),
            "neon"      => ("#0B0616", "#160D27", "#281343", "#FF4FD8", "#77F7FF", "#F7EFFF", "#B7A6C9", "#43265D", 1.05, 0.96),
            "minimal"   => ("#F4F6F8", "#E4E8EC", "#D2D9E0", "#5D7186", "#263646", "#24313D", "#71808E", "#CDD4DB", 0.92, 0.92),
            _           => default
        };

        var current = App.ThemeService.ActiveTheme;
        if (current.Name.Equals($"{scene.Name} · World", StringComparison.OrdinalIgnoreCase)
            && current.AccentPrimary.Equals(palette.Item4, StringComparison.OrdinalIgnoreCase))
            return;

        var worldTheme = current.Duplicate();
        worldTheme.Name = $"{scene.Name} · World";
        worldTheme.Description = scene.Description;
        worldTheme.IsBuiltIn = false;
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
