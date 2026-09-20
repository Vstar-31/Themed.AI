using Microsoft.Extensions.Logging;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow
{
    private bool _vibeProvisioningStarted;

    private async Task InitializeVibeProvisioningAsync()
    {
        if (_vibeProvisioningStarted) return;
        _vibeProvisioningStarted = true;

        App.ThemeService.ThemeChanged += OnProvisioningThemeChanged;

        for (var attempt = 0; attempt < 120 && App.SkinManager is null; attempt++)
            await Task.Delay(50);

        if (App.SkinManager is null)
            return;

        App.SkinManager.EnsureVibeFinderSkinsExist();
        await Task.Delay(100);

        var credentialedWidget = FindCredentialedWidget();
        var playlistWidget = App.SkinManager.Skins.FirstOrDefault(s =>
            s.Name.Equals("VibeFinder Playlist", StringComparison.OrdinalIgnoreCase));

        if (playlistWidget is not null && credentialedWidget is not null && !ReferenceEquals(playlistWidget, credentialedWidget))
        {
            CopyTargetToVibeMeasures(credentialedWidget, playlistWidget);
            await App.SkinManager.SaveSkinAsync(playlistWidget);
            _logger.LogInformation("VibeFinder provisioning: copied saved VibeFinder target into Playlist widget");
        }

        var vibeWidget = playlistWidget ?? credentialedWidget ?? App.SkinManager.Skins.FirstOrDefault(s =>
            s.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase));

        if (vibeWidget is not null && !vibeWidget.Enabled)
        {
            await App.SkinManager.SetEnabledAsync(vibeWidget, true);
            _logger.LogInformation("VibeFinder provisioning: enabled {WidgetName} for desktop integration", vibeWidget.Name);
        }

        EnsureVibeFinderPrewarm();

        await App.SceneService.InitializeAsync();
        await EnsureVibeFinderPlacementAsync(vibeWidget);
    }

    private SkinDefinition? FindCredentialedWidget()
    {
        return App.SkinManager?.Skins.FirstOrDefault(s =>
            s.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase) && HasUsableCredentials(s));
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

    private static string? ReadVibeTarget(SkinDefinition skin)
    {
        foreach (var measure in skin.Measures)
        {
            if (measure.Type is MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood)
            {
                var target = measure.Target?.TrimStart('|');
                if (!string.IsNullOrWhiteSpace(target)) return target;
            }
        }
        return null;
    }

    private static void CopyTargetToVibeMeasures(SkinDefinition source, SkinDefinition destination)
    {
        var target = ReadVibeTarget(source);
        if (string.IsNullOrWhiteSpace(target)) return;

        foreach (var measure in destination.Measures)
        {
            if (measure.Type is MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood)
                measure.Target = target;
        }
    }

    private async Task EnsureVibeFinderPlacementAsync(SkinDefinition? vibeWidget)
    {
        if (vibeWidget is null || App.SceneService.Scenes.Count == 0) return;

        var widgetsById = App.SkinManager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var scene in App.SceneService.Scenes.ToList())
        {
            var sceneChanged = false;
            var placement = scene.Widgets.FirstOrDefault(w =>
                w.WidgetId.Equals(vibeWidget.Id, StringComparison.OrdinalIgnoreCase));

            if (placement is null)
            {
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
                    Monitor = "Primary",
                    Definition = vibeWidget.Clone()
                });
                scene.Behavior.ReactToVibeFinder = true;
                sceneChanged = true;
            }
            else if (placement.Definition is null)
            {
                placement.Definition = vibeWidget.Clone();
                sceneChanged = true;
            }

            // Repair snapshots for every other legacy placement that still exists in the current
            // widget library. After this pass, worlds no longer depend on skins.json retaining every
            // historical definition.
            foreach (var widgetPlacement in scene.Widgets)
            {
                if (widgetPlacement.Definition is not null) continue;
                if (!widgetsById.TryGetValue(widgetPlacement.WidgetId, out var currentWidget)) continue;
                widgetPlacement.Definition = currentWidget.Clone();
                sceneChanged = true;
            }

            if (sceneChanged)
                await App.SceneService.UpsertAsync(scene);
        }

        if (App.SceneService.ActiveScene is { } active)
        {
            var result = await App.SkinManager!.ApplySceneAsync(active.Widgets);
            _logger.LogInformation(
                "VibeFinder provisioning: rehydrated active world with {AppliedCount} widgets ({MissingCount} missing definitions)",
                result.Applied,
                result.Missing);
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
