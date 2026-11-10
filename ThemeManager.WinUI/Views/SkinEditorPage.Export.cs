using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private bool _exportingDraft;
    private async void ExportDraft_Click(object sender, RoutedEventArgs args)
    {
        if (_exportingDraft || _assetPickerOpen || _saving) return;
        _exportingDraft = true;
        FinishDrag();
        try
        {
            var review = new ContentDialog
            {
                Title = "Export draft snapshot", XamlRoot = XamlRoot,
                Content = "Export includes the current design and images without saving or enabling this widget. Connection settings are omitted. Review custom text, links and variables before sharing the package.",
                PrimaryButtonText = "Choose destination", CloseButtonText = "Cancel"
            };
            if (await review.ShowAsync() != ContentDialogResult.Primary) return;
            var picker = new FileSavePicker { SuggestedFileName = "ThemedAI-widget", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("Themed.AI widget", new[] { ".themedwidget" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSaveFileAsync();
            if (file is not null && IsLoaded) await ViewModel.ExportDraftAsync(file.Path);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            ViewModel.StatusMessage = "Couldn't open the export destination. Try again; your draft is unchanged.";
        }
        finally { _exportingDraft = false; }
    }
}
