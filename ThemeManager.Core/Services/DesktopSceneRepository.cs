using System.Text.Json;
using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

public sealed class DesktopSceneRepository
{
    private readonly string StorageFolder;
    private readonly string SceneFilePath;
    private readonly string ActiveSceneFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public DesktopSceneRepository(string? storageFolder = null)
    {
        StorageFolder = storageFolder ?? Path.Combine(AppDataPaths.LocalRoot, "ThemedAI");
        SceneFilePath = Path.Combine(StorageFolder, "scenes.json");
        ActiveSceneFilePath = Path.Combine(StorageFolder, "active-scene.json");
    }

    public async Task<List<DesktopScene>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StorageFolder);
        if (!File.Exists(SceneFilePath)) return new();
        await using var stream = File.OpenRead(SceneFilePath);
        return await JsonSerializer.DeserializeAsync<List<DesktopScene>>(stream, JsonOptions, cancellationToken) ?? new();
    }

    public async Task SaveAllAsync(IReadOnlyList<DesktopScene> scenes, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StorageFolder);
        var temp = SceneFilePath + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, scenes, JsonOptions, cancellationToken);
        File.Move(temp, SceneFilePath, true);
    }

    public async Task<string?> LoadActiveSceneIdAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StorageFolder);
        if (!File.Exists(ActiveSceneFilePath)) return null;
        await using var stream = File.OpenRead(ActiveSceneFilePath);
        return await JsonSerializer.DeserializeAsync<string>(stream, JsonOptions, cancellationToken);
    }

    public async Task SaveActiveSceneIdAsync(string? sceneId, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StorageFolder);
        if (sceneId is null)
        {
            if (File.Exists(ActiveSceneFilePath)) File.Delete(ActiveSceneFilePath);
            return;
        }

        var temp = ActiveSceneFilePath + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, sceneId, JsonOptions, cancellationToken);
        File.Move(temp, ActiveSceneFilePath, true);
    }
}
