using Microsoft.UI.Xaml;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage
{
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Rebind only when the cached browser is actually ready. Claim authority after the
        // command sink exists so widget clicks can never be routed to a half-initialized WebView.
        if (VibeFinderWebView.CoreWebView2 is not null)
        {
            BindVisibleEmbedBridge();
            ThemeManager.Integration.Skins.VibeFinderWebState.ClaimVisibleEmbed();
        }

        // Keep the switches synchronized with the active world's saved composition.
        // navigation must never tear down and recreate VFAI windows.
        SyncWidgetTogglesFromActiveWorld();
    }
}
