using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>Compatibility helpers for VibeFinder integrations that still call the legacy credential API.</summary>
public static class VibeFinderCompatibilityExtensions
{
    /// <summary>
    /// Updates every VibeFinder measure that uses credentials/prompt in its target string.
    /// The operation is intentionally fire-and-forget to preserve the legacy void API used by
    /// VibeFinderAIPage; SaveSkinAsync handles persistence and live refresh for enabled widgets.
    /// </summary>
    public static void UpdateVibeFinderCredentials(this SkinManagerService service, string target)
    {
        if (service is null || string.IsNullOrWhiteSpace(target)) return;

        var vibeSkins = service.Skins
            .Where(s => s.Name.StartsWith("VibeFinder", StringComparison.Ordinal))
            .ToList();

        foreach (var skin in vibeSkins)
        {
            bool changed = false;
            foreach (var measure in skin.Measures)
            {
                if (measure.Type is MeasureType.VibeTrackTitle
                    or MeasureType.VibeTrackArtist
                    or MeasureType.VibeMood
                    or MeasureType.VibePlaybackState
                    or MeasureType.VibeTrackProgress)
                {
                    measure.Target = target;
                    changed = true;
                }
            }

            if (changed)
                _ = service.SaveSkinAsync(skin);
        }
    }
}
