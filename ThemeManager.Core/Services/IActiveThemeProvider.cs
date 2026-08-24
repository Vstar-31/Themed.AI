using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>
/// Minimal read-only view of "what theme is active right now" — the one thing measure
/// construction needs from <see cref="ThemeService"/>, without handing out its full CRUD/
/// event/persistence surface to code that only ever wants to read the current theme once
/// per poll.
///
/// Exists so <c>ThemeManager.Integration</c> (e.g. <c>VibeFinderMeasure</c>, via
/// <c>MeasureFactory</c>) can depend on "the active theme" without depending on
/// <c>ThemeManager.WinUI</c> — <see cref="ThemeService"/> itself already lives in
/// <c>ThemeManager.Core</c>, which both <c>Integration</c> and <c>WinUI</c> can see, but
/// handing the whole service down into a single <see cref="Skins.IMeasure"/> felt like more
/// surface than that one call site should be able to touch (rename themes, delete themes,
/// subscribe to <see cref="ThemeService.ThemeListChanged"/>, ...). <see cref="ThemeService"/>
/// satisfies this interface for free — it already exposes <c>ActiveTheme</c> as a public
/// getter — so implementing it is a one-line addition, not a new code path.
/// </summary>
public interface IActiveThemeProvider
{
    /// <summary>The theme currently applied to the app. Never null — falls back to the
    /// built-in default the same way <see cref="ThemeService.ActiveTheme"/> does.</summary>
    CozyTheme ActiveTheme { get; }
}
