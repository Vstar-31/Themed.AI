using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
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

    // Backing field read by the XAML x:Bind — projected from ViewModel.ContrastResults
    // so the DataTemplate can use x:DataType="ContrastResultProxy" without pulling
    // a WinUI reference into the Core layer.
    private IReadOnlyList<ContrastResultProxy> _contrastProxies = Array.Empty<ContrastResultProxy>();
    public IReadOnlyList<ContrastResultProxy> ContrastProxies => _contrastProxies;

    public ThemeEditorPage()
    {
        InitializeComponent();
        ViewModel = new ThemeEditorViewModel(App.ThemeService);

        // Refresh proxies (and tell x:Bind to re-read ContrastProxies) whenever
        // the contrast results change.
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeEditorViewModel.ContrastResults)
                || e.PropertyName == string.Empty
                || e.PropertyName is null)
            {
                RefreshContrastProxies();
            }
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

    // Called by ColorTokenRow.ColorChanged — live update happens through the ViewModel binding.
    private void OnColorTokenChanged(object sender, string newHex) { }

    // ── Contrast proxy refresh ────────────────────────────────────────────────

    private void RefreshContrastProxies()
    {
        _contrastProxies = ContrastResultProxy.FromList(ViewModel.ContrastResults);
        // Bindings.Update() tells the compiled x:Bind engine to re-read all
        // page-level properties (including ContrastProxies) immediately.
        Bindings.Update();
    }
}
