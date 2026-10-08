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
            await App.SkinManager.PersistSkinDataAsync(playlistWidget);
            _logger.LogInformation("VibeFinder provisioning: copied saved VibeFinder target into Playlist widget");
        }

        // VFAI widgets are user-controlled. Provisioning must never silently enable a widget,
        // add it to every Studio world, or apply the active world. Those operations belong to
        // the user's VFAI/Studio controls and were the source of duplicate windows and world
        // preferences being overwritten on startup.
        EnsureVibeFinderPrewarm();

        await App.SceneService.InitializeAsync();
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
