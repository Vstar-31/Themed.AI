using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private IDisposable? _dragEdit;

    private void FinishDrag()
    {
        _dragging = false;
        _dragTarget = null;
        var edit = _dragEdit;
        _dragEdit = null;
        edit?.Dispose();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) { FinishDrag(); ViewModel.Undo(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { FinishDrag(); ViewModel.Redo(); }

    private bool CanUseEditorShortcut()
    {
        if (!IsLoaded || _saving || _assetPickerOpen || _exportingDraft) return false;
        for (var element = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is TextBox or RichEditBox or NumberBox or AutoSuggestBox or ContentDialog or ComboBox) return false;
            if (ReferenceEquals(element, this)) return true;
        }
        // Focus in a popup, dialog, or another page must not edit this draft.
        return false;
    }

    private void UndoShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!CanUseEditorShortcut()) return;
        FinishDrag();
        ViewModel.Undo();
        args.Handled = true;
    }

    private void RedoShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!CanUseEditorShortcut()) return;
        FinishDrag();
        ViewModel.Redo();
        args.Handled = true;
    }

    private void Layer_Click(object sender, RoutedEventArgs e)
    {
        FinishDrag();
        if ((sender as FrameworkElement)?.Tag is string action) ViewModel.ArrangeMeter(action);
    }

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        FinishDrag();
        if ((sender as FrameworkElement)?.Tag is string action)
            ViewModel.AlignMeters(MetersList.SelectedItems.OfType<MeterEditorItem>().ToArray(), action);
    }
}
