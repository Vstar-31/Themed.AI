using Microsoft.Extensions.Logging;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Services;

internal static class WorldActivation
{
    internal sealed record Result(string Status, string? AppearanceStatus);
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task SaveOutgoingLayoutAsync(DesktopScene scene)
    {
        await Gate.WaitAsync();
        try
        {
            if (App.SceneService.ActiveScene?.Id == scene.Id)
                await SaveLayoutAsync(scene);
        }
        finally { Gate.Release(); }
    }

    internal static async Task<Result> ApplyAsync(DesktopScene target)
    {
        await Gate.WaitAsync();
        try
        {
            var manager = App.SkinManager ?? throw new InvalidOperationException("Desktop widgets are still starting. Try again.");
            var previous = App.SceneService.ActiveScene;
            if (previous is not null && !previous.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase))
                await SaveLayoutAsync(previous);

            var theme = target.ThemeSnapshot ?? (await App.ThemeRepository.LoadAllAsync())
                .FirstOrDefault(t => t.Id.Equals(target.ThemeId, StringComparison.OrdinalIgnoreCase));
            var originalLayout = CaptureCurrentWidgetLayout();
            var requested = target.Widgets.Count;
            var committed = false;
            string? appearanceStatus = null;
            (int Applied, int Missing) result = default;
            try
            {
                result = await manager.ApplySceneAsync(target.Widgets);
                var byId = manager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
                var backfilled = false;
                foreach (var placement in target.Widgets)
                {
                    if (placement.Definition is null && byId.TryGetValue(placement.WidgetId, out var skin))
                    {
                        placement.Definition = skin.Clone(skin.Id);
                        backfilled = true;
                    }
                }
                if (backfilled)
                    await App.SceneService.UpsertAsync(target);

                await App.SceneService.SetAppliedSceneAsync(target, async () =>
                {
                    committed = true;
                    appearanceStatus = await ApplyAppearanceAsync(target, theme);
                });
            }
            catch when (!committed)
            {
                try { await manager.ApplySceneAsync(originalLayout); }
                catch (Exception rollbackError)
                {
                    App.LoggerFactory.CreateLogger(nameof(WorldActivation)).LogError(rollbackError,
                        "Could not restore the previous widget layout after a world failed to apply");
                }
                throw;
            }
            catch (Exception error)
            {
                App.LoggerFactory.CreateLogger(nameof(WorldActivation)).LogWarning(error,
                    "World {WorldId} was saved but its desktop update did not finish", target.Id);
                appearanceStatus = "World activated. Some desktop updates could not finish.";
            }
            var status = result.Missing > 0
                ? $"Applied {result.Applied}/{requested} widgets · {result.Missing} unavailable"
                : $"Applied {result.Applied} widgets to desktop ✓";
            return new Result(status, appearanceStatus);
        }
        finally { Gate.Release(); }
    }

    private static async Task<string?> ApplyAppearanceAsync(DesktopScene target, CozyTheme? theme)
    {
        var messages = new List<string>();
        if (theme is not null && !await App.ThemeService.SetActiveThemeAsync(theme))
            messages.Add("Windows could not apply all requested theme settings.");

        var wallpaper = File.Exists(target.WallpaperPath) ? target.WallpaperPath
            : theme?.ApplyToWallpaper == true ? theme.WallpaperPath : null;
        if (File.Exists(wallpaper))
            messages.Add(await App.SystemIntegrator.ApplyWallpaperAsync(wallpaper)
                ? $"Applied ✓  {Path.GetFileName(wallpaper)} · bound to {target.Name}"
                : $"Windows rejected the wallpaper: {Path.GetFileName(wallpaper)}");
        return messages.Count == 0 ? null : string.Join(" ", messages);
    }

    private static async Task SaveLayoutAsync(DesktopScene scene)
    {
        if (App.SkinManager is null) return;

        var byId = App.SkinManager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        // Older builds could persist an active badge without ever displaying that world.
        // Do not copy the unrelated desktop's positions or visibility over its saved placements.
        if (scene.Widgets.Any(p => byId.TryGetValue(p.WidgetId, out var skin) && p.Visible != skin.Enabled))
            return;
        var changed = false;

        foreach (var placement in scene.Widgets)
        {
            if (!byId.TryGetValue(placement.WidgetId, out var skin)) continue;

            if (placement.X != skin.X ||
                placement.Y != skin.Y ||
                Math.Abs(placement.Opacity - skin.Opacity) > 0.001 ||
                placement.Visible != skin.Enabled)
                changed = true;

            placement.X = skin.X;
            placement.Y = skin.Y;
            placement.Opacity = Math.Clamp(skin.Opacity, 0, 1);
            placement.Visible = skin.Enabled;
            placement.Definition = skin.Clone(skin.Id);
        }

        if (changed)
            await App.SceneService.UpsertAsync(scene);
    }

    internal static List<SceneWidgetPlacement> CaptureCurrentWidgetLayout() =>
        App.SkinManager?.Skins
            .Where(w => w.Enabled)
            .Select((w, index) => new SceneWidgetPlacement
            {
                WidgetId = w.Id,
                X = w.X,
                Y = w.Y,
                Scale = 1.0,
                Rotation = 0,
                Opacity = Math.Clamp(w.Opacity, 0, 1),
                ZIndex = index,
                Visible = w.Enabled,
                Monitor = "Primary",
                Definition = w.Clone()
            })
            .ToList() ?? new List<SceneWidgetPlacement>();

}
