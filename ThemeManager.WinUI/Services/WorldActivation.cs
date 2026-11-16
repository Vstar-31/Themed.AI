using Microsoft.Extensions.Logging;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Services;

internal static class WorldActivation
{
    internal sealed record Result(string Status, string? WallpaperStatus);
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
            try
            {
                var result = await manager.ApplySceneAsync(target.Widgets);
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

                var wallpaperStatus = await ApplyAppearanceAsync(target, theme);
                await App.SceneService.SetAppliedSceneAsync(target);
                var status = result.Missing > 0
                    ? $"Applied {result.Applied}/{requested} widgets · {result.Missing} unavailable"
                    : $"Applied {result.Applied} widgets to desktop ✓";
                return new Result(status, wallpaperStatus);
            }
            catch
            {
                try { await manager.ApplySceneAsync(originalLayout); }
                catch (Exception rollbackError)
                {
                    App.LoggerFactory.CreateLogger(nameof(WorldActivation)).LogError(rollbackError,
                        "Could not restore the previous widget layout after a world failed to apply");
                }
                throw;
            }
        }
        finally { Gate.Release(); }
    }

    private static async Task<string?> ApplyAppearanceAsync(DesktopScene target, CozyTheme? theme)
    {
        if (theme is not null)
        {
            App.ThemeService.SetActiveTheme(theme);
            if (theme.ApplyToWindowsApps)
                await App.SystemIntegrator.ApplyWindowsThemeAsync(IsLightPalette(theme.BackgroundBase));
            if (theme.ApplyToSystemAccent)
                await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(theme.AccentPrimary));
        }

        var wallpaper = File.Exists(target.WallpaperPath) ? target.WallpaperPath
            : theme?.ApplyToWallpaper == true ? theme.WallpaperPath : null;
        if (string.IsNullOrWhiteSpace(wallpaper) || !File.Exists(wallpaper)) return null;
        return await App.SystemIntegrator.ApplyWallpaperAsync(wallpaper)
            ? $"Applied ✓  {Path.GetFileName(wallpaper)} · bound to {target.Name}"
            : $"Windows rejected the wallpaper: {Path.GetFileName(wallpaper)}";
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

    internal static bool IsLightPalette(string hex)
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

}
