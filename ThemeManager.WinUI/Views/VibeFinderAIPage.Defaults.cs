using Microsoft.UI.Xaml;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage
{
    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var skin in _skinManager.Skins.Where(s => s.Name.StartsWith("VibeFinder", StringComparison.Ordinal)))
        {
            if (skin.Variables.TryGetValue("designVersion", out var version) && version == DefaultWidgetCatalog.DesignVersion)
                continue;

            var enabled = skin.Enabled;
            var x = skin.X;
            var y = skin.Y;
            DefaultWidgetCatalog.RebuildVibeFinder(skin);
            skin.Enabled = enabled;
            skin.X = x;
            skin.Y = y;
            await _skinManager.SaveSkinAsync(skin);
        }
    }
}
