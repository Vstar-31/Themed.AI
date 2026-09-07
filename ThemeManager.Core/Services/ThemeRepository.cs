using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

public sealed class ThemeRepository
{
    private static readonly string StorageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThemedAI");
    private static readonly string ThemesFilePath = Path.Combine(StorageFolder, "themes.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<List<CozyTheme>> LoadAllAsync()
    {
        EnsureStorageFolderExists();
        if (!File.Exists(ThemesFilePath)) return await SeedDefaultsAsync();
        try
        {
            await using var stream = File.OpenRead(ThemesFilePath);
            var themes = await JsonSerializer.DeserializeAsync<List<CozyTheme>>(stream, JsonOptions) ?? new();
            EnsureBuiltInThemesPresent(themes);
            return themes;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            try { File.Move(ThemesFilePath, ThemesFilePath + ".bak", overwrite: true); } catch (IOException) { }
            return await SeedDefaultsAsync();
        }
    }

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public async Task SaveAllAsync(IEnumerable<CozyTheme> themes)
    {
        EnsureStorageFolderExists();
        await _saveLock.WaitAsync();
        try
        {
            var list = themes.ToList();
            var tempPath = $"{ThemesFilePath}.{Guid.NewGuid():N}.tmp";
            try { await WriteAndMoveAsync(list, tempPath); }
            catch (IOException)
            {
                await Task.Delay(200);
                await WriteAndMoveAsync(list, tempPath);
            }
        }
        finally { _saveLock.Release(); }
    }

    private async Task WriteAndMoveAsync(List<CozyTheme> list, string tempPath)
    {
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
        File.Move(tempPath, ThemesFilePath, overwrite: true);
    }

    public async Task ExportThemeAsync(CozyTheme theme, string filePath)
    {
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, theme, JsonOptions);
    }

    public async Task<CozyTheme?> ImportThemeAsync(string filePath)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var theme = await JsonSerializer.DeserializeAsync<CozyTheme>(stream, JsonOptions);
            if (theme is null) return null;
            theme.Id = Guid.NewGuid().ToString();
            theme.IsBuiltIn = false;
            return theme;
        }
        catch { return null; }
    }

    private static void EnsureStorageFolderExists() => Directory.CreateDirectory(StorageFolder);

    private static void EnsureBuiltInThemesPresent(List<CozyTheme> themes)
    {
        foreach (var builtIn in DefaultThemeCatalog.CreateAll())
        {
            var existing = themes.Find(t => t.Id == builtIn.Id);
            if (existing is null)
            {
                themes.Add(builtIn);
                continue;
            }

            // Built-ins are product presets, not user-owned theme documents. Refresh their visual
            // tokens on upgrade so an older installation receives the repaired typography palette.
            existing.Name = builtIn.Name;
            existing.Description = builtIn.Description;
            existing.IsBuiltIn = true;
            existing.BackgroundBase = builtIn.BackgroundBase;
            existing.BackgroundAlt = builtIn.BackgroundAlt;
            existing.Surface = builtIn.Surface;
            existing.AccentPrimary = builtIn.AccentPrimary;
            existing.AccentStrong = builtIn.AccentStrong;
            existing.TextPrimary = builtIn.TextPrimary;
            existing.TextMuted = builtIn.TextMuted;
            existing.BorderSubtle = builtIn.BorderSubtle;
            existing.CornerRadiusScale = builtIn.CornerRadiusScale;
            existing.DensityScale = builtIn.DensityScale;
        }
    }

    private async Task<List<CozyTheme>> SeedDefaultsAsync()
    {
        var defaults = DefaultThemeCatalog.CreateAll().ToList();
        await SaveAllAsync(defaults);
        return defaults;
    }
}
