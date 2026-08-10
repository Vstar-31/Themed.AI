using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.WinUI.Views;
using Windows.Graphics;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();

        // Navigate to Themes list on startup.
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);
    }

    // ── Title bar – extend content into title bar for a macOS-like look ──────
    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null); // Let the whole window be draggable; sidebar acts as drag area.

        var appWindow = AppWindow;
        if (AppWindowTitleBar.IsCustomizationSupported() && appWindow is not null)
        {
            var tb = appWindow.TitleBar;
            tb.ExtendsContentIntoTitleBar = true;
            tb.ButtonBackgroundColor         = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor  = Colors.Transparent;
            tb.ButtonHoverBackgroundColor     = Windows.UI.Color.FromArgb(0x20, 0, 0, 0);
            tb.ButtonPressedBackgroundColor   = Windows.UI.Color.FromArgb(0x30, 0, 0, 0);
            tb.ButtonForegroundColor          = Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x2A, 0x20);
        }

        // Resize to a comfortable default.
        AppWindow?.Resize(new SizeInt32(1100, 720));
    }

    // ── Navigation ─────────────────────────────────────────────────────────────

    private void NavThemes_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);
    }

    private void NavVibe_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(VibePage));
        SetActiveNav(NavVibe);
    }

    private void NavPreview_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ThemeEditorPage));
        SetActiveNav(NavPreview);
    }

    private void NavSystem_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SystemIntegrationPage));
        SetActiveNav(NavSystem);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        SetActiveNav(NavSettings);
    }

    private void NavWidgets_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SkinsPage));
        SetActiveNav(NavWidgets);
    }

    private void NavWidgetVibe_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(WidgetGeneratorPage));
        SetActiveNav(NavWidgetVibe);
    }

    /// <summary>Swaps the visual state of sidebar buttons.</summary>
    private void SetActiveNav(Button active)
    {
        Button[] all = [NavThemes, NavVibe, NavPreview, NavSystem, NavWidgets, NavWidgetVibe, NavSettings];
        foreach (var btn in all)
        {
            btn.Style = btn == active
                ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemActiveStyle"]
                : (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemStyle"];
        }
    }
}
