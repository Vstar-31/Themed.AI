using System.IO.Compression;
using System.Text.Json;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.Services;

/// <summary>Bounded world archives containing a scene and independently validated widget packages.</summary>
public sealed class WorldPackageService
{
    public const long MaxBytes = 64 * 1024 * 1024;
    private const int MaxWidgets = 32;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _root;
    public WorldPackageService(string? root = null) => _root = Path.GetFullPath(root ?? Path.Combine(AppDataPaths.LocalRoot, "ThemedAI", "WorldAssets"));

    public async Task ExportAsync(DesktopScene source, IEnumerable<SkinDefinition> widgets, CozyTheme? theme, string path)
    {
        var scene = JsonSerializer.Deserialize<DesktopScene>(JsonSerializer.Serialize(source, Json), Json)!;
        Validate(scene);
        scene.ThemeSnapshot = theme is null ? scene.ThemeSnapshot : JsonSerializer.Deserialize<CozyTheme>(JsonSerializer.Serialize(theme, Json), Json);
        if (string.IsNullOrWhiteSpace(scene.WallpaperPath) && scene.ThemeSnapshot is { ApplyToWallpaper: true, WallpaperPath: { } originalWallpaper }) scene.WallpaperPath = originalWallpaper;
        if (scene.ThemeSnapshot is { } palette) { palette.WallpaperPath = null; palette.ApplyToWallpaper = false; }
        var inventory = widgets.ToDictionary(w => w.Id, StringComparer.OrdinalIgnoreCase);
        var target = Path.GetFullPath(path);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var scratch = Path.Combine(_root, ".export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            using (var archive = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                long total = 0;
                for (int i = 0; i < scene.Widgets.Count; i++)
                {
                    var placement = scene.Widgets[i];
                    var skin = placement.Definition ?? inventory.GetValueOrDefault(placement.WidgetId)
                        ?? throw new InvalidDataException("A world widget is missing its definition.");
                    if (skin.Meters.Any(m => string.Equals(m.ImagePath, target, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Export cannot overwrite a source image.");
                    var widgetPath = Path.Combine(scratch, i + ".themedwidget");
                    await new SkinPackageService().ExportAsync(skin, widgetPath);
                    using (var nested = ZipFile.OpenRead(widgetPath)) total += nested.Entries.Sum(e => e.Length);
                    if (total > MaxBytes) throw new InvalidDataException("World assets exceed 64 MiB.");
                    archive.CreateEntryFromFile(widgetPath, $"widgets/{i:D3}.themedwidget");
                    placement.Definition = null;
                }
                if (!string.IsNullOrWhiteSpace(scene.WallpaperPath))
                {
                    var wallpaper = Path.GetFullPath(scene.WallpaperPath);
                    if (!Path.IsPathFullyQualified(scene.WallpaperPath) || !SkinPackageService.IsSupportedImage(wallpaper) || wallpaper.Equals(target, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Wallpaper must be a local raster image distinct from the export file.");
                    var size = new FileInfo(wallpaper).Length;
                    if (size > SkinPackageService.MaxAssetBytes || total + size > MaxBytes) throw new InvalidDataException("Wallpaper exceeds world package limits.");
                    scene.WallpaperPath = "wallpaper" + Path.GetExtension(wallpaper).ToLowerInvariant();
                    archive.CreateEntryFromFile(wallpaper, scene.WallpaperPath);
                }
                scene.ThemeId = "";
                var bytes = JsonSerializer.SerializeToUtf8Bytes(scene, Json);
                if (bytes.Length > SkinPackageService.MaxManifestBytes) throw new InvalidDataException("World manifest exceeds 1 MiB.");
                await using var output = archive.CreateEntry("world.json").Open();
                await output.WriteAsync(bytes);
            }
            if (new FileInfo(temp).Length > MaxBytes) throw new InvalidDataException("World package exceeds 64 MiB.");
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); Directory.Delete(scratch, true); }
    }

    // The caller persists the scene without activating it. Assets are isolated under the new scene ID.
    public async Task<DesktopScene> ImportAsync(string path)
    {
        await using var file = File.OpenRead(path);
        if (file.Length > MaxBytes) throw new InvalidDataException("World package exceeds 64 MiB.");
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaxWidgets + 2) throw new InvalidDataException("Too many world entries.");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            if (!entries.TryAdd(entry.FullName, entry) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || entry.Length < 0 || entry.Length > MaxBytes - expanded)
                throw new InvalidDataException("Invalid or oversized world archive.");
            expanded += entry.Length;
        }
        if (!entries.TryGetValue("world.json", out var manifest)) throw new InvalidDataException("Missing world.json.");
        DesktopScene scene;
        try { scene = JsonSerializer.Deserialize<DesktopScene>(await ReadAsync(manifest, SkinPackageService.MaxManifestBytes), Json) ?? throw new InvalidDataException("Empty world."); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid world manifest.", ex); }
        Validate(scene);
        var expected = new HashSet<string>(StringComparer.Ordinal) { "world.json" };
        for (int i = 0; i < scene.Widgets.Count; i++) expected.Add($"widgets/{i:D3}.themedwidget");
        if (!string.IsNullOrEmpty(scene.WallpaperPath))
        {
            if (scene.WallpaperPath != "wallpaper" + Path.GetExtension(scene.WallpaperPath) || !SkinPackageService.IsSupportedImage(scene.WallpaperPath))
                throw new InvalidDataException("Invalid wallpaper entry.");
            expected.Add(scene.WallpaperPath);
        }
        if (!expected.SetEquals(entries.Keys)) throw new InvalidDataException("Missing or unexpected world entries.");
        scene.Id = Guid.NewGuid().ToString();
        scene.Name += " (Imported)";
        scene.RequiresExplicitActivation = true;
        scene.ThemeId = "";
        scene.Behavior.AutoSwitch = false;
        if (scene.ThemeSnapshot is { } theme)
        {
            theme.Id = Guid.NewGuid().ToString(); theme.IsBuiltIn = false;
            theme.WallpaperPath = null; theme.ApplyToWallpaper = false;
            theme.CustomTokens ??= new();
            PaletteQualityValidator.ValidateAndRepair(theme);
        }
        var destination = Path.Combine(_root, scene.Id);
        Directory.CreateDirectory(destination);
        try
        {
            long total = 0;
            for (int i = 0; i < scene.Widgets.Count; i++)
            {
                var widgetPath = Path.Combine(destination, i + ".themedwidget");
                await File.WriteAllBytesAsync(widgetPath, await ReadAsync(entries[$"widgets/{i:D3}.themedwidget"], MaxBytes));
                using (var nested = ZipFile.OpenRead(widgetPath))
                {
                    foreach (var entry in nested.Entries)
                    {
                        if (entry.Length < 0 || entry.Length > MaxBytes - total) throw new InvalidDataException("Expanded world exceeds 64 MiB.");
                        total += entry.Length;
                    }
                }
                var skin = await new SkinPackageService(Path.Combine(destination, "widgets")).ImportAsync(widgetPath);
                File.Delete(widgetPath);
                scene.Widgets[i].WidgetId = skin.Id;
                scene.Widgets[i].Definition = skin;
            }
            if (!string.IsNullOrEmpty(scene.WallpaperPath))
            {
                var bytes = await ReadAsync(entries[scene.WallpaperPath], Math.Min(SkinPackageService.MaxAssetBytes, MaxBytes - total));
                var wallpaper = Path.Combine(destination, scene.WallpaperPath);
                await File.WriteAllBytesAsync(wallpaper, bytes); scene.WallpaperPath = wallpaper;
            }
            return scene;
        }
        catch { Directory.Delete(destination, true); throw; }
    }

    public async Task<DesktopScene> ImportToLibraryAsync(string path, DesktopSceneService library)
    {
        var scene = await ImportAsync(path);
        try { await library.UpsertAsync(scene); return scene; }
        catch
        {
            // Keep assets if an event handler failed after the inventory committed.
            if (!library.Scenes.Any(s => s.Id == scene.Id))
                Directory.Delete(Path.Combine(_root, scene.Id), true);
            throw;
        }
    }

    private static async Task<byte[]> ReadAsync(ZipArchiveEntry entry, long limit)
    {
        if (entry.Length > limit) throw new InvalidDataException("Package entry exceeds its size limit.");
        await using var input = entry.Open(); using var output = new MemoryStream();
        var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer)) != 0)
        { if (output.Length + read > limit) throw new InvalidDataException("Package entry exceeds its size limit."); await output.WriteAsync(buffer.AsMemory(0, read)); }
        return output.ToArray();
    }

    private static void Validate(DesktopScene scene)
    {
        if (scene.SchemaVersion != 2 || string.IsNullOrWhiteSpace(scene.Name) || scene.Widgets is null || scene.Widgets.Count > MaxWidgets || scene.Widgets.Any(w => w is null) || scene.Tags is null || scene.Tags.Any(t => t is null) || scene.Effects is null || scene.Effects.Any(e => e is null || e.Parameters is null) || scene.Behavior is null)
            throw new InvalidDataException("Unsupported or malformed world.");
        foreach (var w in scene.Widgets)
            if (!double.IsFinite(w.X) || !double.IsFinite(w.Y) || !double.IsFinite(w.Scale) || w.Scale <= 0 || w.Scale > 10 || !double.IsFinite(w.Rotation) || !double.IsFinite(w.Opacity) || w.Opacity < 0 || w.Opacity > 1)
                throw new InvalidDataException("Invalid widget placement.");
    }
}
