using Microsoft.UI.Xaml;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage
{
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Loaded can fire after CoreWebView2 initialization (and after another page briefly
        // owned/released the global command bridge). Reclaim visible authority and rebind the
        // command sink every time the page becomes visible.
        ThemeManager.Integration.Skins.VibeFinderWebState.ClaimVisibleEmbed();
        BindVisibleEmbedBridge();

        // Keep the switches synchronized with the active world's saved composition.
        // navigation must never tear down and recreate VFAI windows.
        SyncWidgetTogglesFromActiveWorld();
    }
}
