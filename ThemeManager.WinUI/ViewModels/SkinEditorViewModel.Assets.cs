using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed partial class SkinEditorViewModel
{
    public bool ApplyImageAsset(string path, string? targetMeterId)
    {
        var asset = WidgetAssetLibrary.Inspect(path);
        if (!asset.Available) { StatusMessage = asset.Status; return false; }
        var target = targetMeterId is null ? null : Meters.FirstOrDefault(m => m.Definition.Id == targetMeterId && m.IsImage);
        if (targetMeterId is not null && target is null)
        {
            StatusMessage = "The selected image meter changed. Choose a meter and try again.";
            return false;
        }
        using var edit = BeginEdit();
        if (target is null) { AddMeter(MeterKind.Image); target = SelectedMeter!; }
        target.ImagePath = asset.Path;
        SelectedMeter = target;
        StatusMessage = "Image added to the draft. Save to update the widget.";
        return true;
    }
}
