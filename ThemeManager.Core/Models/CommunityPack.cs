using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Models;

/// <summary>
/// A bundle of themes and widget skins, shipped as one JSON file and browsable in the Phase 8
/// Local Gallery. Deliberately just a plain data bag over the same <see cref="CozyTheme"/> and
/// <see cref="SkinDefinition"/> types everything else in the app already reads/writes/serializes
/// — a pack is not a new format, it's a named collection of things the rest of the app already
/// knows how to apply. That's what makes it "local": no server, no upload/moderation pipeline,
/// no schema of its own to version — just a JSON file sitting in a known folder that
/// <see cref="Services.GalleryService"/> reads on startup, the same way
/// <see cref="Services.ThemeRepository"/>/<see cref="Services.SkinRepository"/> already read
/// their own JSON files. Anyone can hand-write one (or export one, once Phase 8's "Import/export
/// v2" bullet gets there) without touching any app code.
/// </summary>
public sealed class CommunityPack
{
    /// <summary>Display name of the pack itself (e.g. "Starter Pack"), not any one theme/widget
    /// inside it.</summary>
    public string Name { get; set; } = "Untitled Pack";

    public string Description { get; set; } = string.Empty;

    /// <summary>Free-text attribution — a person's name, a handle, "Themed.AI Team", etc. Not
    /// validated or linked anywhere; purely a label shown in the gallery.</summary>
    public string Author { get; set; } = string.Empty;

    public List<CozyTheme> Themes { get; set; } = new();

    /// <summary>Widget skins included in this pack. Named <c>Widgets</c> rather than <c>Skins</c>
    /// deliberately — "skin" is this codebase's internal name for the same concept
    /// (<see cref="SkinDefinition"/>), but every user-facing string in the app (the nav item, the
    /// page title, "+ New Widget") already says "widget", so the gallery's own JSON matches what
    /// a person hand-authoring a pack would actually call the thing they're adding.</summary>
    public List<SkinDefinition> Widgets { get; set; } = new();
}
