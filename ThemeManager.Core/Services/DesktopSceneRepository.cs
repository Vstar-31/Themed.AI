using System.Text.Json;
using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

public sealed class DesktopSceneRepository
{
    private static readonly string StorageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThemedAI");
    private static readonly string SceneFilePath = Path.Combine(StorageFolder, "scenes.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

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
}
