using Microsoft.UI.Xaml;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage
{
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Keep the switches synchronized with the active world's saved composition.
        // Widget layout/design migration is handled once by SkinManagerService provisioning;
        // navigation must never tear down and recreate VFAI windows.
        SyncWidgetTogglesFromActiveWorld();
    }
}
