using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private void DuplicateShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!CanUseEditorShortcut()) return;
        FinishDrag();
        if (_selectedLayerKey is not { } key) return;
        if (ViewModel.DuplicateLayer(key) is { } copyKey)
        {
            RefreshLayerTree();
            SelectLayerNode(copyKey);
        }
        args.Handled = true;
    }

    private void NudgeShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!CanUseEditorShortcut()) return;
        FinishDrag();
        if (_selectedLayerKey is not { } key) return;
        double step = sender.Modifiers.HasFlag(VirtualKeyModifiers.Shift) ? 10 : 1;
        double dx = sender.Key == VirtualKey.Left ? -step : sender.Key == VirtualKey.Right ? step : 0;
        double dy = sender.Key == VirtualKey.Up ? -step : sender.Key == VirtualKey.Down ? step : 0;
        var selected = MetersList.SelectedItems.OfType<MeterEditorItem>().ToArray();
        if (key.StartsWith("meter:", StringComparison.Ordinal) && selected.Length > 1)
            ViewModel.NudgeMeters(selected, dx, dy);
        else ViewModel.NudgeLayer(key, dx, dy);
        args.Handled = true;
    }

    private void RecoverDraft_Click(object sender, RoutedEventArgs args)
    {
        if (_saving || _assetPickerOpen || _exportingDraft) return;
        FinishDrag();
        ViewModel.RecoverAsNewDraft(App.SkinManager.Skins.Select(s => s.Name));
    }
}
