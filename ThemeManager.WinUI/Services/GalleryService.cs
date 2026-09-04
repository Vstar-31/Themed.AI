using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

/// <summary>
/// Loads <see cref="CommunityPack"/> files from a folder — Phase 8's "Local Gallery": browse
/// community-submitted themes/widgets bundled as a local JSON pack. Deliberately not a network
/// client — "local" is the point, not a placeholder for "not implemented yet". A pack is just a
/// JSON file; dropping a new one into the folder this points at is the entire authoring/publishing
/// story for now, no server or moderation queue to build. Read-only: this service only loads packs
/// for browsing. Actually adding a pack's theme/widget to the person's own library goes through
/// the same repositories/services everything else in the app already uses
/// (<see cref="ThemeService.SaveThemeAsync"/> for a theme; the WinUI project's
/// <c>SkinManagerService.AddGeneratedSkinAsync</c> for a widget, since skin *lifecycle* — window
/// creation, enable/disable — lives in WinUI, not Core) — a pack item becomes an ordinary
/// theme/widget the moment it's added, indistinguishable from one the person built themselves,
/// so nothing about "came from a pack" needs tracking once it's in.
/// </summary>
public sealed class GalleryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _packsFolder;
    private readonly List<CommunityPack> _packs = new();

    /// <summary>Packs successfully loaded on the last <see cref="LoadAsync"/> call. A pack that
    /// failed to parse is skipped, not surfaced here — see <see cref="LoadErrors"/>.</summary>
    public IReadOnlyList<CommunityPack> Packs => _packs;

    /// <summary>File name → exception message, for any pack file that existed but failed to
    /// parse. Checked by the WinUI project's gallery page to show a status line rather than
    /// silently showing fewer packs than the folder actually contains; empty on a clean load.</summary>
    public IReadOnlyDictionary<string, string> LoadErrors => _loadErrors;
    private readonly Dictionary<string, string> _loadErrors = new();

    /// <param name="packsFolder">
    /// Where to look for <c>*.json</c> pack files. The caller decides the path deliberately —
    /// this service doesn't know or care whether that's a bundled app-content folder
    /// (<c>AppContext.BaseDirectory</c>-relative, what the one shipped starter pack uses) or a
    /// user-writable one (LocalApplicationData, for a future "install a pack someone sent me"
    /// flow) — either is just a folder of JSON files to this class.
    /// </param>
    public GalleryService(string packsFolder)
    {
        _packsFolder = packsFolder;
    }

    /// <summary>(Re)loads every <c>*.json</c> file in the packs folder, replacing whatever was
    /// previously loaded. Missing folder is not an error — an app with no packs shipped or
    /// installed yet should show an empty gallery, not fail to start.</summary>
    public async Task LoadAsync()
    {
        _packs.Clear();
        _loadErrors.Clear();

        if (!Directory.Exists(_packsFolder)) return;

        // Ordered so the gallery's pack order is stable across runs (directory enumeration order
        // isn't guaranteed) and so a person naming files "01-starter.json", "02-seasonal.json"
        // etc. gets predictable control over display order without this service needing an
        // explicit "order" field on the pack itself.
        foreach (var path in Directory.EnumerateFiles(_packsFolder, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                var pack = await JsonSerializer.DeserializeAsync<CommunityPack>(stream, JsonOptions);
                if (pack is not null) _packs.Add(pack);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // One malformed/locked pack shouldn't take down the whole gallery — every other
                // valid file in the folder should still load. Recorded rather than swallowed so
                // GalleryPage can tell the person a pack failed instead of just showing fewer
                // cards than they expected with no explanation.
                _loadErrors[Path.GetFileName(path)] = ex.Message;
            }
        }
    }

    /// <summary>Returns an independent copy of a pack theme, ready to hand to
    /// <see cref="ThemeService.SaveThemeAsync"/>. A JSON round-trip rather than a field-by-field
    /// copy or <see cref="CozyTheme.Duplicate"/> deliberately: <c>Duplicate()</c> renames to
    /// "X (Copy)", which is right when duplicating a theme you already own but wrong for
    /// importing one that should keep the name its author gave it; a round-trip also guarantees
    /// a genuinely independent <see cref="CozyTheme.CustomTokens"/> dictionary rather than a
    /// shared reference back into the still-loaded <see cref="CommunityPack"/>, so adding a theme
    /// twice (or editing it afterward) can never mutate what the gallery itself displays.</summary>
    public static CozyTheme PrepareThemeForImport(CozyTheme packTheme)
    {
        var copy = JsonSerializer.Deserialize<CozyTheme>(JsonSerializer.Serialize(packTheme, JsonOptions), JsonOptions)!;
        copy.Id = Guid.NewGuid().ToString();
        copy.IsBuiltIn = false;
        copy.LastModified = DateTimeOffset.UtcNow;
        return copy;
    }

    /// <summary>Returns an independent copy of a pack widget, ready to hand to
    /// <c>SkinManagerService.AddGeneratedSkinAsync</c>. Same JSON-round-trip reasoning as
    /// <see cref="PrepareThemeForImport"/> — a widget's <c>Meters</c>/<c>Measures</c> lists need
    /// to be genuinely separate instances, not shared with the loaded pack, before the person
    /// starts dragging/editing it. Forces <c>Enabled = false</c> regardless of what the pack file
    /// says — a gallery add is a "here it is, take a look" moment, not "immediately put this on
    /// the person's desktop"; every widget this app creates any other way (the editor's "+ New
    /// Widget", a Vibe-generated one) starts disabled for the same reason.</summary>
    public static SkinDefinition PrepareWidgetForImport(SkinDefinition packWidget)
    {
        var copy = JsonSerializer.Deserialize<SkinDefinition>(JsonSerializer.Serialize(packWidget, JsonOptions), JsonOptions)!;
        copy.Id = Guid.NewGuid().ToString();
        copy.Enabled = false;
        foreach (var meter in copy.Meters) meter.Id = Guid.NewGuid().ToString();
        return copy;
    }
}
