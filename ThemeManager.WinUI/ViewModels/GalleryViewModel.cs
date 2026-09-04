using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Backs <see cref="Views.GalleryPage"/> — Phase 8's Local Gallery. Unlike
/// <see cref="ThemesViewModel"/>/the skins page, there's no live-changing source list to mirror
/// here: <see cref="GalleryService"/> loads its packs once at startup and doesn't change out from
/// under a running session (no file-watcher, no "someone else editing the same pack" concern —
/// it's read-only bundled content), so <see cref="Packs"/> is just <c>_gallery.Packs</c> exposed
/// directly rather than an ObservableCollection kept in sync via an event handler.
/// </summary>
public sealed class GalleryViewModel : ViewModelBase
{
    private readonly GalleryService _gallery;
    private readonly ThemeService _themeService;
    private readonly SkinManagerService _skinManager;

    public IReadOnlyList<CommunityPack> Packs => _gallery.Packs;

    /// <summary>True when no packs loaded at all — bound to the empty-state message in
    /// GalleryPage. A real bool (not a Count) deliberately, so it can go through the existing
    /// BoolToVisibilityConverter every other page already uses rather than needing its own.</summary>
    public bool IsEmpty => _gallery.Packs.Count == 0;

    /// <summary>True when at least one pack file existed but failed to parse — see
    /// <see cref="GalleryService.LoadErrors"/>. Checked once at construction, same as
    /// <see cref="Packs"/>: this page doesn't re-load packs while open.</summary>
    public bool HasLoadErrors => _gallery.LoadErrors.Count > 0;

    public IReadOnlyDictionary<string, string> LoadErrors => _gallery.LoadErrors;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public GalleryViewModel(GalleryService gallery, ThemeService themeService, SkinManagerService skinManager)
    {
        _gallery = gallery;
        _themeService = themeService;
        _skinManager = skinManager;
    }

    /// <summary>Adds a copy of a pack theme to the person's own theme list. Does not activate
    /// it — matches how "+ New Theme"/"Duplicate" on the Themes page behave too; the person picks
    /// it up from there when they're ready, same flow either way.</summary>
    public async Task AddThemeAsync(CozyTheme packTheme)
    {
        var copy = GalleryService.PrepareThemeForImport(packTheme);
        await _themeService.SaveThemeAsync(copy);
        StatusMessage = $"Added \"{copy.Name}\" to your themes.";
    }

    /// <summary>Adds a copy of a pack widget to the person's own widget list, disabled (see
    /// <see cref="GalleryService.PrepareWidgetForImport"/> for why). They enable it from the
    /// Widgets page like any other.</summary>
    public async Task AddWidgetAsync(SkinDefinition packWidget)
    {
        var copy = GalleryService.PrepareWidgetForImport(packWidget);
        await _skinManager.AddGeneratedSkinAsync(copy);
        StatusMessage = $"Added \"{copy.Name}\" to your widgets (disabled — turn it on from Widgets).";
    }
}
