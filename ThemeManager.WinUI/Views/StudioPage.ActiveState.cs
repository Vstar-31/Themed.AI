using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private bool _activeStateUiInstalled;
    private bool _autoSwitchUiUpdating;

    private async void StudioPage_ActiveStateLoaded(object sender, RoutedEventArgs e)
    {
        if (!_activeStateUiInstalled)
        {
            _activeStateUiInstalled = true;
            App.SceneService.ScenesChanged += OnScenesChanged;
            App.SceneService.ActiveSceneChanged += OnActiveSceneChanged;
            App.ThemeService.ThemeChanged += ThemeService_ThemeChanged;
            App.SceneService.ActiveSceneChanged += ActiveStateSceneChanged;
        }

        _studioInitializing = true;
        try
        {
            _autoApplyAesthetic = LoadAutoApplyAestheticPreference();
            AutoApplyAestheticToggle.IsOn = _autoApplyAesthetic;

            await App.SceneService.InitializeAsync();
            await App.StartWorldRuntimeAsync();
            await SeedStarterWorldsAsync();
            Refresh();

            UpdateSetActiveButtonState();
            UpdateAutoSwitchUi();

            // Opening Studio is a preview. Only an explicit selection/apply action changes the desktop.

        }
        finally
        {
            _studioInitializing = false;
            SyncStudioState();
        }
    }
    private async void StudioPage_ActiveStateUnloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var active = App.SceneService.ActiveScene;
            if (active is not null && App.SkinManager is not null)
                await SaveCurrentSceneLayoutAsync(active);
        }
        catch (Exception ex)
        {
            App.LoggerFactory.CreateLogger<StudioPage>().LogDebug(ex, "Studio: failed to persist active world layout on unload");
        }

        if (_activeStateUiInstalled)
        {
            App.SceneService.ScenesChanged -= OnScenesChanged;
            App.SceneService.ActiveSceneChanged -= OnActiveSceneChanged;
            App.ThemeService.ThemeChanged -= ThemeService_ThemeChanged;
            App.SceneService.ActiveSceneChanged -= ActiveStateSceneChanged;
            _activeStateUiInstalled = false;
        }
    }

    private async void ActiveStateSceneChanged(object? sender, DesktopScene? scene)
    {
        if (DispatcherQueue.HasThreadAccess) UpdateSetActiveButtonState();
        else DispatcherQueue.TryEnqueue(UpdateSetActiveButtonState);
        if (scene is not null && ShouldAutoApplyWorld(scene) && !_sceneApplyBusy)
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
        finally { _autoSwitchUiUpdating = false; }
    }

    private async void AutoSwitchToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_autoSwitchUiUpdating || _studioInitializing || _selected is null || !_selected.Behavior.ReactToVibeFinder) return;
        _selected.Behavior.AutoSwitch = AutoSwitchToggle.IsOn;
        await App.SceneService.UpsertAsync(_selected);
        AutoSwitchStatusText.Text = AutoSwitchToggle.IsOn
            ? "Automatically moves between vibe-matched worlds."
            : "Manual world selection only.";
        ApplyStatus.Text = AutoSwitchToggle.IsOn ? "Vibe auto-switch enabled ✓" : "Vibe auto-switch disabled.";
    }

    private async Task ApplyWorldPaletteAsync(DesktopScene scene)
    {
        if (scene.ThemeSnapshot is { } importedPalette)
        {
            PaletteQualityValidator.ValidateAndRepair(importedPalette);
            App.ThemeService.SetActiveTheme(importedPalette);
            await App.SystemIntegrator.ApplyWindowsThemeAsync(IsLightPalette(importedPalette.BackgroundBase));
            await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(importedPalette.AccentPrimary));
            return;
        }
        var tag = scene.Tags.FirstOrDefault(t =>
            t.Equals("nocturnal", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("cozy", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("hud", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("neon", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("minimal", StringComparison.OrdinalIgnoreCase));
        if (tag is null) return;

        // Prompt-generated worlds own the palette selected by the generator. Do not replace
        // that user-specific theme with a tag preset when the world is rehydrated or applied.
        if (scene.Tags.Any(t => t.Equals("prompt-generated", StringComparison.OrdinalIgnoreCase)) &&
            App.ThemeService.Themes.FirstOrDefault(t => t.Id.Equals(scene.ThemeId, StringComparison.OrdinalIgnoreCase)) is { } generatedTheme)
        {
            var generatedQuality = PaletteQualityValidator.ValidateAndRepair(generatedTheme);
            if (generatedQuality.Repairs.Count > 0)
                await App.ThemeService.SaveThemeAsync(generatedTheme);

            App.ThemeService.SetActiveTheme(generatedTheme);
            await App.SystemIntegrator.ApplyWindowsThemeAsync(IsLightPalette(generatedTheme.BackgroundBase));
            await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(generatedTheme.AccentPrimary));
            return;
        }

        var worldThemeId = $"world-{tag.ToLowerInvariant()}";
        var worldTheme = App.ThemeService.Themes.FirstOrDefault(t => t.Id.Equals(worldThemeId, StringComparison.OrdinalIgnoreCase));
        bool isNewWorldTheme = worldTheme is null;
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
                "cozy" => ("#F5F1EA", "#D7C9B8", "#B2967D", "#7D5A44", "#4A342A", "#3B2A20", "#5E4D42", "#E0D5C7", 1.00, 1.05),
                "hud" => ("#071014", "#0D1B22", "#11303A", "#62E6F7", "#B8F7FF", "#E9FDFF", "#8FB7BE", "#214751", 0.92, 0.94),
                "neon" => ("#0B0616", "#160D27", "#281343", "#FF4FD8", "#77F7FF", "#F7EFFF", "#B7A6C9", "#43265D", 1.05, 0.96),
                "minimal" => ("#F4F6F8", "#E4E8EC", "#D2D9E0", "#5D7186", "#263646", "#24313D", "#5A6875", "#CDD4DB", 0.92, 0.92),
                _ => default
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
        }

        // Legacy and built-in Studio palettes use the same quality gate as NLP-generated palettes.
        // This repairs old saved worlds too, not only newly created presets.
        bool? preferDark = tag is "nocturnal" or "hud" or "neon"
            ? true
            : tag is "cozy" or "minimal"
                ? false
                : null;
        var quality = PaletteQualityValidator.ValidateAndRepair(worldTheme, preferDark);
        if (isNewWorldTheme || quality.Repairs.Count > 0)
            await App.ThemeService.SaveThemeAsync(worldTheme);

        if (!scene.ThemeId.Equals(worldTheme.Id, StringComparison.OrdinalIgnoreCase))
        {
            scene.ThemeId = worldTheme.Id;
            await App.SceneService.UpsertAsync(scene);
        }

        App.ThemeService.SetActiveTheme(worldTheme);

        // Map the world's palette to the native Windows Light/Dark app + system mode so supported
        // Windows apps such as File Explorer follow the same visual direction.
        await App.SystemIntegrator.ApplyWindowsThemeAsync(IsLightPalette(worldTheme.BackgroundBase));
        await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(worldTheme.AccentPrimary));
    }

    private static bool IsLightPalette(string hex)
    {
        try
        {
            var c = App.HexToColor(CozyTheme.NormalizeHex(hex));
            static double Linear(byte channel)
            {
                double v = channel / 255.0;
                return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            double luminance = 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
            return luminance >= 0.42;
        }
        catch
        {
            return true;
        }
    }

    private void UpdateSetActiveButtonState()
    {
        if (SetActiveWorldButton is null) return;
        var activeId = App.SceneService.ActiveScene?.Id;
        var active = _selected?.Id is not null && activeId is not null && string.Equals(_selected.Id, activeId, StringComparison.OrdinalIgnoreCase);
        SetActiveWorldButton.Content = active ? "Active world ✓" : "Activate this world";
        SetActiveWorldButton.IsEnabled = _selected is not null && !active;
    }

    private void SetSelectedWorldActive_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ApplyStatus.Text = "Select a world first.";
            return;
        }

        // This action only changes which world is marked active. Visual application remains
        // explicit (Apply to desktop) or is controlled by the aesthetic toggle.
        App.SceneService.SetActiveScene(_selected);
        UpdateAutoSwitchUi();
        UpdateSetActiveButtonState();
        SyncStudioState();
    }
}
