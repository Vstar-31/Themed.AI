using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

public sealed record WidgetRecoveryDraft(Guid Id, DateTimeOffset UpdatedAt, SkinDefinition Definition)
{
    public override string ToString() => $"{Definition.Name} — {UpdatedAt.ToLocalTime():g}";
}

/// <summary>Local checkpoints only. Loading never changes the saved widget inventory.</summary>
public sealed class WidgetDraftRepository
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private const int MaxBytes = 16 * 1024 * 1024;
    private readonly string _folder;
    public WidgetDraftRepository(string? folder = null) => _folder = folder ?? Path.Combine(AppDataPaths.LocalRoot, "ThemedAI", "Drafts");
    private string FilePath(Guid id) => Path.Combine(_folder, id.ToString("N") + ".json");

    public async Task SaveAsync(Guid id, SkinDefinition definition)
    {
        // Capture before awaiting: later editor mutations cannot alter this checkpoint.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new WidgetRecoveryDraft(id, DateTimeOffset.UtcNow, definition), Json);
        if (bytes.Length > MaxBytes) throw new IOException("The recovery draft exceeds 16 MB.");
        await Gate.WaitAsync();
        var temporary = FilePath(id) + ".tmp";
        try
        {
            Directory.CreateDirectory(_folder);
            await File.WriteAllBytesAsync(temporary, bytes);
            File.Move(temporary, FilePath(id), true);
        }
        finally { Gate.Release(); }
    }

    public async Task DeleteAsync(Guid id)
    {
        await Gate.WaitAsync();
        try { File.Delete(FilePath(id)); }
        catch (DirectoryNotFoundException) { /* No checkpoint directory means nothing to remove. */ }
        finally { Gate.Release(); }
    }

    public async Task<(IReadOnlyList<WidgetRecoveryDraft> Drafts, int Unreadable)> ListAsync()
    {
        await Gate.WaitAsync();
        try
        {
            var result = new List<WidgetRecoveryDraft>();
            int unreadable = 0;
            if (!Directory.Exists(_folder)) return (result, 0);
            foreach (var path in Directory.EnumerateFiles(_folder, "*.json"))
            {
                try
                {
                    if (new FileInfo(path).Length > MaxBytes) { unreadable++; continue; }
                    var draft = JsonSerializer.Deserialize<WidgetRecoveryDraft>(await File.ReadAllBytesAsync(path), Json);
                    if (draft?.Definition is not { } skin || skin.Meters is null || skin.Measures is null ||
                        skin.Meters.Any(m => m is null || string.IsNullOrEmpty(m.Id)) || skin.Measures.Any(m => m is null) ||
                        (skin.Groups?.Any(g => g is null) ?? false) || (skin.Tags?.Any(t => t is null) ?? false) ||
                        !Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id) || id != draft.Id)
                    { unreadable++; continue; }
                    result.Add(draft);
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
                { unreadable++; }
            }
            return (result.OrderByDescending(d => d.UpdatedAt).ToArray(), unreadable);
        }
        finally { Gate.Release(); }
    }
}
