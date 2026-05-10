using System.Text.Json;

namespace ThemeManager.Core.Services;

/// <summary>
/// Persists general app preferences to %LOCALAPPDATA%\ThemedAI\settings.json.
/// Separate from themes.json so user preferences survive a theme backup/restore.
/// </summary>
public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThemedAI", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ── Preferences ───────────────────────────────────────────────────────────

    /// <summary>ID of the theme that was active when the app last closed.</summary>
    public string LastActiveThemeId { get; set; } = "cozy-default";

    /// <summary>Whether the app registers itself to run at Windows startup.</summary>
    public bool LaunchAtStartup { get; set; } = false;

    /// <summary>
    /// Whether the Vibe page's "Pipeline Insights" panel is expanded.
    /// Persisted so power users don't have to re-open it every launch.
    /// </summary>
    public bool VibeInsightsPanelOpen { get; set; } = true;

    // ── Persistence ───────────────────────────────────────────────────────────

    public static async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                await using var s = File.OpenRead(SettingsPath);
                var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(s, JsonOpts);
                if (loaded is not null) return loaded;
            }
        }
        catch { /* first run or corrupt file – return defaults */ }
        return new AppSettings();
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var tmp = SettingsPath + ".tmp";
        await using (var s = File.Create(tmp))
            await JsonSerializer.SerializeAsync(s, this, JsonOpts);
        File.Move(tmp, SettingsPath, overwrite: true);
    }
}
