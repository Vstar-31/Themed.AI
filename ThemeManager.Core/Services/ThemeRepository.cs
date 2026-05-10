using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>
/// Loads and saves <see cref="CozyTheme"/> objects to/from a JSON file in LocalApplicationData.
/// Thread-safe for async callers; internal list is only mutated on the caller's thread.
/// </summary>
public sealed class ThemeRepository
{
    // ── Storage path ────────────────────────────────────────────────────────
    private static readonly string StorageFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThemedAI");

    private static readonly string ThemesFilePath =
        Path.Combine(StorageFolder, "themes.json");

    // ── JSON options (shared, immutable) ────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented          = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all themes from disk.
    /// If no file exists, seeds with the built-in Cozy Café default and persists it.
    /// </summary>
    public async Task<List<CozyTheme>> LoadAllAsync()
    {
        EnsureStorageFolderExists();

        if (!File.Exists(ThemesFilePath))
            return await SeedDefaultsAsync();

        try
        {
            await using var stream = File.OpenRead(ThemesFilePath);
            var themes = await JsonSerializer.DeserializeAsync<List<CozyTheme>>(stream, JsonOptions)
                         ?? new List<CozyTheme>();

            // Ensure the built-in theme is always present and marked correctly.
            EnsureBuiltInThemePresent(themes);
            return themes;
        }
        catch (JsonException)
        {
            // Corrupt file – back it up and re-seed.
            File.Move(ThemesFilePath, ThemesFilePath + ".bak", overwrite: true);
            return await SeedDefaultsAsync();
        }
    }

    /// <summary>Persists the full list of themes to disk atomically (write-then-rename).</summary>
    public async Task SaveAllAsync(IEnumerable<CozyTheme> themes)
    {
        EnsureStorageFolderExists();

        var list = themes.ToList();
        var tempPath = ThemesFilePath + ".tmp";

        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);

        // Atomic replace – avoids a corrupt file on crash mid-write.
        File.Move(tempPath, ThemesFilePath, overwrite: true);
    }

    /// <summary>Exports a single theme to a user-chosen file path.</summary>
    public async Task ExportThemeAsync(CozyTheme theme, string filePath)
    {
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, theme, JsonOptions);
    }

    /// <summary>Imports a theme from a JSON file. Returns null on parse failure.</summary>
    public async Task<CozyTheme?> ImportThemeAsync(string filePath)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var theme = await JsonSerializer.DeserializeAsync<CozyTheme>(stream, JsonOptions);
            if (theme is null) return null;

            // Give the imported theme a fresh ID to avoid collisions.
            theme.Id        = Guid.NewGuid().ToString();
            theme.IsBuiltIn = false;
            return theme;
        }
        catch
        {
            return null;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void EnsureStorageFolderExists() =>
        Directory.CreateDirectory(StorageFolder);

    private static void EnsureBuiltInThemePresent(List<CozyTheme> themes)
    {
        var builtIn = themes.Find(t => t.Id == "cozy-default");
        if (builtIn is null)
            themes.Insert(0, CozyDefaults.CreateDefault());
        else
            builtIn.IsBuiltIn = true; // Enforce flag in case it was serialized as false.
    }

    private async Task<List<CozyTheme>> SeedDefaultsAsync()
    {
        var defaults = new List<CozyTheme> { CozyDefaults.CreateDefault() };
        await SaveAllAsync(defaults);
        return defaults;
    }
}
