using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private bool _activeStateUiInstalled;
    private bool _autoSwitchUiUpdating;

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
        UpdateAutoSwitchUi();

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
                    // Initialization may still be hydrating persisted scenes; retry briefly.
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

    private async void ActiveStateSceneChanged(object? sender, DesktopScene? scene)
    {
        if (DispatcherQueue.HasThreadAccess) UpdateSetActiveButtonState();
        else DispatcherQueue.TryEnqueue(UpdateSetActiveButtonState);

        UpdateAutoSwitchUi();

        if (scene is not null)
            await ApplyWorldPaletteAsync(scene);
    }

    private void UpdateAutoSwitchUi()
    {
        if (AutoSwitchToggle is null) return;

        _autoSwitchUiUpdating = true;
        try
        {
            AutoSwitchToggle.IsOn = _selected?.Behavior.AutoSwitch == true;
            AutoSwitchToggle.IsEnabled = _selected is not null && _selected.Behavior.ReactToVibeFinder;
            AutoSwitchStatusText.Text = AutoSwitchToggle.IsOn
                ? "Automatically moves between vibe-matched worlds."
                : "Manual world selection only.";
        }
        finally
        {
            _autoSwitchUiUpdating = false;
        }
    }

    private async void AutoSwitchToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_autoSwitchUiUpdating || _selected is null) return;
        if (!_selected.Behavior.ReactToVibeFinder)
        {
            UpdateAutoSwitchUi();
            return;
        }

        _selected.Behavior.AutoSwitch = AutoSwitchToggle.IsOn;
        await App.SceneService.UpsertAsync(_selected);
        AutoSwitchStatusText.Text = AutoSwitchToggle.IsOn
            ? "Automatically moves between vibe-matched worlds."
            : "Manual world selection only.";
        ApplyStatus.Text = AutoSwitchToggle.IsOn
            ? "Vibe auto-switch enabled ✓"
            : "Vibe auto-switch disabled.";
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
                "cozy"      => ("#F5F1EA", "#D7C9B8", "#B2967D", "#7D5A44", "#4A342A", "#3B2A20", "#5E4D42", "#E0D5C7", 1.00, 1.05),
                "hud"       => ("#071014", "#0D1B22", "#11303A", "#62E6F7", "#B8F7FF", "#E9FDFF", "#8FB7BE", "#214751", 0.92, 0.94),
                "neon"      => ("#0B0616", "#160D27", "#281343", "#FF4FD8", "#77F7FF", "#F7EFFF", "#B7A6C9", "#43265D", 1.05, 0.96),
                "minimal"   => ("#F4F6F8", "#E4E8EC", "#D2D9E0", "#5D7186", "#263646", "#24313D", "#5A6875", "#CDD4DB", 0.92, 0.92),
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
            UpdateAutoSwitchUi();
            UpdateSetActiveButtonState();
        }
    }

    private void UpdateSetActiveButtonState()
    {
        if (SetActiveWorldButton is null) return;

        var activeId = App.SceneService.ActiveScene?.Id;
        var active = _selected?.Id is not null
            && activeId is not null
            && string.Equals(_selected.Id, activeId, StringComparison.OrdinalIgnoreCase);

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

        await ApplySelectedWorldAsync();
        UpdateAutoSwitchUi();
        UpdateSetActiveButtonState();
    }
}
