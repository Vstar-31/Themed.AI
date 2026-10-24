using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

public sealed class SkinRepository
{
    private readonly string StorageFolder;
    private readonly string SkinsFilePath;

    public SkinRepository(string? storageFolder = null)
    {
        StorageFolder = storageFolder ?? Path.Combine(ThemeManager.Core.Services.AppDataPaths.LocalRoot, "ThemedAI");
        SkinsFilePath = Path.Combine(StorageFolder, "skins.json");
    }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<List<SkinDefinition>> LoadAllAsync()
    {
        try
        {
            EnsureStorageFolderExists();
            if (!File.Exists(SkinsFilePath)) return await SeedDefaultsAsync();

            await using var stream = File.OpenRead(SkinsFilePath);
            var skins = await JsonSerializer.DeserializeAsync<List<SkinDefinition>>(stream, JsonOptions) ?? new();
            if (Migrate(skins))
            {
                try
                {
                    await SaveAllAsync(skins);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Migration is best-effort. A locked/ACL-protected skins.json must never stop
                    // the desktop customizer from launching with the already-valid in-memory model.
                }
            }
            return skins;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Storage problems are recoverable. Keep the app alive and fall back to canonical
            // defaults in memory; the next successful edit can persist them.
            try
            {
                if (File.Exists(SkinsFilePath))
                    File.Move(SkinsFilePath, SkinsFilePath + ".bak", overwrite: true);
            }
            catch (Exception backupEx) when (backupEx is IOException or UnauthorizedAccessException) { }

            try
            {
                return SkinDefaults.CreateAllDefaults().ToList();
            }
            catch
            {
                return new List<SkinDefinition>();
            }
        }
    }

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public async Task SaveAllAsync(IEnumerable<SkinDefinition> skins)
    {
        try { await SaveAllCheckedAsync(skins); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Existing edits keep their in-memory fallback when storage is unavailable.
        }
    }

    /// <summary>Use for imports, which must not report success unless persistence succeeds.</summary>
    public async Task SaveAllCheckedAsync(IEnumerable<SkinDefinition> skins)
    {
        EnsureStorageFolderExists();
        await _saveLock.WaitAsync();
        var tempPath = $"{SkinsFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var snapshot = skins.ToList();
            try { await WriteAndMoveAsync(snapshot, tempPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(200);
                await WriteAndMoveAsync(snapshot, tempPath);
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            finally { _saveLock.Release(); }
        }
    }

    private async Task WriteAndMoveAsync(List<SkinDefinition> list, string tempPath)
    {
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
        File.Move(tempPath, SkinsFilePath, overwrite: true);
    }

    private void EnsureStorageFolderExists() => Directory.CreateDirectory(StorageFolder);

    private static bool Migrate(List<SkinDefinition> skins)
    {
        bool changed = false;
        var defaults = SkinDefaults.CreateAllDefaults().ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var skin in skins)
        {
            if (skin.Id.StartsWith("builtin-", StringComparison.OrdinalIgnoreCase) && skin.SchemaVersion < 3 && defaults.TryGetValue(skin.Id, out var canonical))
            {
                var enabled = skin.Enabled;
                var x = skin.X;
                var y = skin.Y;
                var opacity = skin.Opacity;
                skin.Name = canonical.Name;
                skin.Description = canonical.Description;
                skin.Author = canonical.Author;
                skin.Tags = canonical.Tags.ToList();
                skin.Width = canonical.Width;
                skin.Height = canonical.Height;
                skin.Opacity = opacity <= 0 ? canonical.Opacity : opacity;
                skin.ClickThrough = canonical.ClickThrough;
                skin.AlwaysOnTop = canonical.AlwaysOnTop;
                skin.Locked = skin.Locked;
                skin.DesktopLayer = canonical.DesktopLayer;
                skin.UpdateIntervalMs = canonical.UpdateIntervalMs;
                skin.Measures = canonical.Measures;
                skin.Meters = canonical.Meters;
                skin.Variables = canonical.Variables;
                skin.SchemaVersion = 3;
                skin.Enabled = enabled;
                skin.X = x;
                skin.Y = y;
                changed = true;
            }
            else if (skin.SchemaVersion < 3)
            {
                skin.SchemaVersion = 3;
                changed = true;
            }

            // v4 changes the desktop window policy: widgets are normal desktop windows by default,
            // not global always-on-top windows. A future explicit user choice is stored in
            // AlwaysOnTop; only legacy skins created before this policy existed are migrated.
            if (skin.SchemaVersion < 4)
            {
                skin.AlwaysOnTop = false;
                skin.SchemaVersion = 4;
                changed = true;
            }

            // v5 introduces composition groups. Existing widgets need no structural migration:
            // the field deserializes as an empty list and all current meters remain at widget scope.
            if (skin.SchemaVersion < 5)
            {
                skin.SchemaVersion = 5;
                changed = true;
            }

            // v6 adds per-meter states and monitor anchoring. Both are optional additions, so older
            // widgets need only the version bump; they keep their static look and saved X/Y.
            if (skin.SchemaVersion < 6)
            {
                skin.SchemaVersion = 6;
                changed = true;
            }

            if (skin.Groups is null)
            {
                skin.Groups = new List<WidgetGroupDefinition>();
                changed = true;
            }

            if (skin.Meters is null)
            {
                skin.Meters = new List<MeterDefinition>();
                changed = true;
            }
            foreach (var meter in skin.Meters)
            {
                if (meter.States is null)
                {
                    meter.States = new List<MeterStateDefinition>();
                    changed = true;
                }
            }

            if (skin.Anchor is { Responsive: null } anchor)
            {
                anchor.Responsive = new List<WidgetResponsiveRule>();
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
                    meter.Opacity = 1.0;
                    changed = true;
                }
                if (meter.Kind == MeterKind.String)
                {
                    var minimumHeight = Math.Ceiling(meter.FontSize * 1.45);
                    if (meter.Height < minimumHeight)
                    {
                        meter.Height = minimumHeight;
                        changed = true;
                    }
                    if (meter.Width < 32)
                    {
                        meter.Width = 32;
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }

    private async Task<List<SkinDefinition>> SeedDefaultsAsync()
    {
        var defaults = SkinDefaults.CreateAllDefaults();
        try
        {
            await SaveAllAsync(defaults);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return defaults;
    }
}