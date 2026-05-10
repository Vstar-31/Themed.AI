using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;
using ThemeManager.WinUI.ViewModels;
using Windows.System;

namespace ThemeManager.WinUI.Views;

public sealed partial class ThemeEditorPage : Page
{
    public ThemeEditorViewModel ViewModel { get; }

    public ThemeEditorPage()
    {
        InitializeComponent();
        ViewModel = new ThemeEditorViewModel(App.ThemeService);

        // Keep ContrastResults projected into proxies whenever they change.
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ThemeEditorViewModel.ContrastResults)
                               or string.Empty)
                RefreshContrastProxies();
        };

        RefreshContrastProxies();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is CozyTheme theme)
            ViewModel.LoadTheme(theme);
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource
                       .GetKeyStateForCurrentThread(VirtualKey.Control);
        bool isCtrl = ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (!isCtrl) return;

        switch (e.Key)
        {
            case VirtualKey.Z:
                e.Handled = true;
                ViewModel.Undo();
                break;
            case VirtualKey.Y:
                e.Handled = true;
                ViewModel.Redo();
                break;
            case VirtualKey.S:
                e.Handled = true;
                _ = ViewModel.SaveAsync();
                break;
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(ThemesPage));
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
        => await ViewModel.SaveAsync();

    private void RevertButton_Click(object sender, RoutedEventArgs e)
        => ViewModel.RevertToDefault();

    private void UndoButton_Click(object sender, RoutedEventArgs e)
        => ViewModel.Undo();

    private void RedoButton_Click(object sender, RoutedEventArgs e)
        => ViewModel.Redo();

    private void OnColorTokenChanged(object sender, string newHex) { /* live via VM */ }

    // ── Contrast proxy refresh ────────────────────────────────────────────────

    private void RefreshContrastProxies()
    {
        // The XAML DataTemplate is typed to ContrastResultProxy — project here.
        // We bind a separate property on the page instead of the VM to avoid
        // pulling a WinUI reference into the Core layer.
        _contrastProxies = ContrastResultProxy.FromList(ViewModel.ContrastResults);
    }

    // Backing field read by the XAML x:Bind (property projection pattern).
    private IReadOnlyList<ContrastResultProxy> _contrastProxies = Array.Empty<ContrastResultProxy>();
    public  IReadOnlyList<ContrastResultProxy> ContrastProxies => _contrastProxies;
}
