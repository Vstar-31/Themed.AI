using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

public sealed class SkinRepository
{
    private static readonly string StorageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThemedAI");
    private static readonly string SkinsFilePath = Path.Combine(StorageFolder, "skins.json");
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
        try
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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A reader/another instance/AV scanner may briefly own the target. Retry once,
                    // then deliberately leave the in-memory state authoritative instead of failing
                    // the UI operation or crashing startup.
                    await Task.Delay(200);
                    try
                    {
                        await WriteAndMoveAsync(list, tempPath);
                    }
                    catch (Exception retryEx) when (retryEx is IOException or UnauthorizedAccessException)
                    {
                    }
                }
            }
            finally { _saveLock.Release(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Storage is persistence, not a prerequisite for rendering the desktop.
        }
    }

    private static async Task WriteAndMoveAsync(List<SkinDefinition> list, string tempPath)
    {
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
        File.Move(tempPath, SkinsFilePath, overwrite: true);
    }

    private static void EnsureStorageFolderExists() => Directory.CreateDirectory(StorageFolder);

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
