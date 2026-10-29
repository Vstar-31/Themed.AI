using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinsPage
{
    private bool _recoveryOpen;
    private async void RecoveryDrafts_Click(object sender, RoutedEventArgs e)
    {
        if (_recoveryOpen || _packageOperation) return;
        _recoveryOpen = true;
        try
        {
            var repository = new WidgetDraftRepository();
            var (drafts, unreadable) = await repository.ListAsync();
            if (drafts.Count == 0)
            {
                ViewModel.StatusMessage = unreadable > 0 ? "Recovery files could not be read. They have been kept on disk." : "No recovery drafts. Unsaved editor work is checkpointed automatically.";
                return;
            }
            var list = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = drafts, MaxHeight = 320 };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = "Recover opens an unsaved copy. Nothing runs or replaces a saved widget until you choose Save.", TextWrapping = TextWrapping.Wrap });
            if (unreadable > 0) panel.Children.Add(new TextBlock { Text = $"{unreadable} unreadable checkpoint(s) kept on disk.", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(list);
            var dialog = new ContentDialog { Title = "Recovery drafts", Content = panel, PrimaryButtonText = "Recover copy", SecondaryButtonText = "Discard selected", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
            dialog.IsPrimaryButtonEnabled = false;
            dialog.IsSecondaryButtonEnabled = false;
            list.SelectionChanged += (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled = list.SelectedItem is WidgetRecoveryDraft;
                dialog.IsSecondaryButtonEnabled = dialog.IsPrimaryButtonEnabled;
            };
            list.Loaded += (_, _) => list.SelectedIndex = 0;
            var result = await dialog.ShowAsync();
            if (list.SelectedItem is not WidgetRecoveryDraft selected) return;
            if (result == ContentDialogResult.Primary)
            {
                var copy = WidgetRemix.Create(selected.Definition, App.SkinManager.Skins.Select(s => s.Name));
                Frame.Navigate(typeof(SkinEditorPage), new SkinEditorDraft(copy, selected.Id));
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var confirm = new ContentDialog { Title = "Discard recovery draft?", Content = $"Remove the checkpoint for {selected.Definition.Name}? Saved widgets are unchanged.", PrimaryButtonText = "Discard", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                { await repository.DeleteAsync(selected.Id); ViewModel.StatusMessage = "Recovery checkpoint discarded."; }
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { ViewModel.StatusMessage = "Couldn't read or update recovery drafts. Existing checkpoints have been kept where possible."; }
        finally { _recoveryOpen = false; }
    }
}
