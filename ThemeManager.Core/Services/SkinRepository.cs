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

            // Schema migrations are intentionally performed at the repository boundary. The UI and
            // runtime therefore only ever see the newest in-memory shape, while users can keep old
            // skins indefinitely and still import/edit them safely.
            if (Migrate(skins))
                await SaveAllAsync(skins);

            return skins;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Corrupt or locked file – back it up (best-effort) and re-seed.
            try { File.Move(SkinsFilePath, SkinsFilePath + ".bak", overwrite: true); }
            catch (IOException) { /* backup failed – still safe to re-seed */ }
            return await SeedDefaultsAsync();
        }
    }

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>Persists the full list of skins to disk atomically (write-then-rename).</summary>
    /// <exception cref="IOException">Thrown after one retry if the file is locked or disk is full.</exception>
    public async Task SaveAllAsync(IEnumerable<SkinDefinition> skins)
    {
        EnsureStorageFolderExists();

        await _saveLock.WaitAsync();
        try
        {
            var list = skins.ToList();
            var tempPath = $"{SkinsFilePath}.{Guid.NewGuid():N}.tmp";

            try
            {
                await WriteAndMoveAsync(list, tempPath);
            }
            catch (IOException)
            {
                await Task.Delay(200);
                await WriteAndMoveAsync(list, tempPath);
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task WriteAndMoveAsync(List<SkinDefinition> list, string tempPath)
    {
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);

        File.Move(tempPath, SkinsFilePath, overwrite: true);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void EnsureStorageFolderExists() =>
        Directory.CreateDirectory(StorageFolder);

    private static bool Migrate(List<SkinDefinition> skins)
    {
        bool changed = false;
        foreach (var skin in skins)
        {
            if (skin.SchemaVersion < 2)
            {
                skin.SchemaVersion = 2;
                changed = true;
            }

            if (skin.Tags is null)
            {
                skin.Tags = new List<string>();
                changed = true;
            }

            if (skin.Variables is null)
            {
                skin.Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                changed = true;
            }

            foreach (var measure in skin.Measures)
            {
                if (string.IsNullOrWhiteSpace(measure.Id))
                {
                    measure.Id = Guid.NewGuid().ToString();
                    changed = true;
                }
            }

            foreach (var meter in skin.Meters)
            {
                if (string.IsNullOrWhiteSpace(meter.Id))
                {
                    meter.Id = Guid.NewGuid().ToString();
                    changed = true;
                }
                if (meter.Opacity <= 0)
                {
                    // Old files did not have per-meter opacity; zero means "missing" rather than
                    // "invisible" for the migration. Explicitly invisible meters can still be
                    // authored as a value below 0.001 after migration.
                    meter.Opacity = 1.0;
                    changed = true;
                }
            }
        }
        return changed;
    }

    private async Task<List<SkinDefinition>> SeedDefaultsAsync()
    {
        var defaults = SkinDefaults.CreateAllDefaults();
        await SaveAllAsync(defaults);
        return defaults;
    }
}
