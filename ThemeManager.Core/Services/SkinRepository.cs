using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

/// <summary>
/// Loads and saves <see cref="SkinDefinition"/> objects to/from a JSON file in LocalApplicationData.
/// Deliberately mirrors <see cref="ThemeRepository"/>'s storage pattern (same folder, same
/// atomic write-then-rename technique, same JSON options) so the two persistence stores behave
/// identically and are easy to reason about together.
/// </summary>
public sealed class SkinRepository
{
    // ── Storage path (same folder ThemeRepository uses, sibling file) ──────
    private static readonly string StorageFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThemedAI");

    private static readonly string SkinsFilePath =
        Path.Combine(StorageFolder, "skins.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all skins from disk. If no file exists yet, seeds the three built-in
    /// starter widgets (Clock, System Monitor, Uptime) and persists them.
    /// </summary>
    public async Task<List<SkinDefinition>> LoadAllAsync()
    {
        EnsureStorageFolderExists();

        if (!File.Exists(SkinsFilePath))
            return await SeedDefaultsAsync();

        try
        {
            await using var stream = File.OpenRead(SkinsFilePath);
            var skins = await JsonSerializer.DeserializeAsync<List<SkinDefinition>>(stream, JsonOptions)
                        ?? new List<SkinDefinition>();
            return skins;
        }
        catch (JsonException)
        {
            // Corrupt file – back it up (so nothing is silently lost) and re-seed, exactly
            // like ThemeRepository does for themes.json.
            File.Move(SkinsFilePath, SkinsFilePath + ".bak", overwrite: true);
            return await SeedDefaultsAsync();
        }
    }

    /// <summary>Persists the full list of skins to disk atomically (write-then-rename).</summary>
    public async Task SaveAllAsync(IEnumerable<SkinDefinition> skins)
    {
        EnsureStorageFolderExists();

        var list = skins.ToList();
        var tempPath = SkinsFilePath + ".tmp";

        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);

        File.Move(tempPath, SkinsFilePath, overwrite: true);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void EnsureStorageFolderExists() =>
        Directory.CreateDirectory(StorageFolder);

    private async Task<List<SkinDefinition>> SeedDefaultsAsync()
    {
        var defaults = SkinDefaults.CreateAllDefaults();
        await SaveAllAsync(defaults);
        return defaults;
    }
}
