using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed partial class SkinEditorViewModel
{
    public async Task<bool> ExportDraftAsync(string path, CancellationToken cancellationToken = default)
    {
        // Export an independent snapshot; never change history, the saved baseline, or live widgets.
        var snapshot = _working.Clone();
        snapshot.Name = Name.Trim();
        try
        {
            await new SkinPackageService().ExportAsync(snapshot, path, cancellationToken);
            StatusMessage = "Exported a draft snapshot with its images. Save is still needed to apply unsaved edits.";
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled. Your draft is unchanged.";
            return false;
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusMessage = ex is System.IO.InvalidDataException ? ex.Message :
                "Couldn't export this draft. Check its image files, destination access and available storage. Your draft is still here.";
            return false;
        }
    }
}
