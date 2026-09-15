using Microsoft.UI.Xaml;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow
{
    private bool _vibeProvisioningStarted;

    private async void MainWindow_VibeProvisioningLoaded(object sender, RoutedEventArgs e)
    {
        if (_vibeProvisioningStarted) return;
        _vibeProvisioningStarted = true;

        App.ThemeService.ThemeChanged += OnProvisioningThemeChanged;

        // MainWindow is created before SkinManagerService is initialized by App.OnLaunched.
        for (var attempt = 0; attempt < 120 && App.SkinManager is null; attempt++)
            await Task.Delay(50);

        if (App.SkinManager is null)
            return;

        App.SkinManager.EnsureVibeFinderSkinsExist();
        await Task.Delay(100);

        var vibeWidget = PickProvisioningWidget();
        if (vibeWidget is not null && !vibeWidget.Enabled)
        {
            await App.SkinManager.SetEnabledAsync(vibeWidget, true);
            _logger.LogInformation("VibeFinder provisioning: enabled {WidgetName} for desktop integration", vibeWidget.Name);
        }

        EnsureVibeFinderPrewarm();

        await App.SceneService.InitializeAsync();
        await EnsureVibeFinderPlacementAsync(vibeWidget);
    }

    private SkinDefinition? PickProvisioningWidget()
    {
        if (App.SkinManager is null) return null;

        // Prefer the richer Playlist widget, but preserve credentials from an existing
        // VibeFinder skin when it is the only one carrying saved credentials.
        var playlist = App.SkinManager.Skins.FirstOrDefault(s =>
            s.Name.Equals("VibeFinder Playlist", StringComparison.OrdinalIgnoreCase));
        if (playlist is not null && HasUsableCredentials(playlist))
            return playlist;

        var withCredentials = App.SkinManager.Skins.FirstOrDefault(s =>
            s.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase) && HasUsableCredentials(s));
        return withCredentials ?? playlist ?? App.SkinManager.Skins.FirstOrDefault(s =>
            s.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUsableCredentials(SkinDefinition skin)
    {
        foreach (var measure in skin.Measures)
        {
            if (measure.Type is not (MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood))
                continue;
            var target = measure.Target?.TrimStart('|');
            var parts = target?.Split('|', 3);
            return parts is { Length: >= 2 }
                   && !string.IsNullOrWhiteSpace(parts[0])
                   && !string.IsNullOrWhiteSpace(parts[1])
                   && !parts[0].Equals("listener", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private async Task EnsureVibeFinderPlacementAsync(SkinDefinition? vibeWidget)
    {
        if (vibeWidget is null || App.SceneService.Scenes.Count == 0) return;

        foreach (var scene in App.SceneService.Scenes.ToList())
        {
            if (scene.Widgets.Any(w => w.WidgetId.Equals(vibeWidget.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            var nextZ = scene.Widgets.Count == 0 ? 0 : scene.Widgets.Max(w => w.ZIndex) + 1;
            scene.Widgets.Add(new SceneWidgetPlacement
            {
                WidgetId = vibeWidget.Id,
                X = 40,
                Y = 360,
                Scale = 1.0,
                Rotation = 0,
                Opacity = 0.94,
                ZIndex = nextZ,
                Visible = true,
                Monitor = "Primary"
            });
            scene.Behavior.ReactToVibeFinder = true;
            await App.SceneService.UpsertAsync(scene);
        }

        if (App.SceneService.ActiveScene is { } active)
        {
            var placement = active.Widgets.FirstOrDefault(w =>
                w.WidgetId.Equals(vibeWidget.Id, StringComparison.OrdinalIgnoreCase));
            if (placement is not null)
            {
                await App.SkinManager!.ApplyScenePlacementAsync(
                    vibeWidget,
                    placement.X,
                    placement.Y,
                    placement.Opacity,
                    placement.Visible);
            }
        }

        _logger.LogInformation("VibeFinder provisioning: ensured widget {WidgetId} is part of {SceneCount} desktop worlds", vibeWidget.Id, App.SceneService.Scenes.Count);
    }

    private void OnProvisioningThemeChanged(object? sender, CozyTheme theme)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsVibeFinderPrewarmActive)
            {
                EnsureVibeFinderPrewarm();
                return;
            }

            RebindVibeFinderPrewarmBridge();
            PushVibePromptAndTrackLimit();
        });
    }
}
