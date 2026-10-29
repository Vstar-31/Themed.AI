using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed partial class SkinEditorViewModel
{
    private bool _isSaving;
    public bool IsSaving { get => _isSaving; private set { if (SetProperty(ref _isSaving, value)) OnPropertyChanged(nameof(SaveState)); } }
    public string SaveState => IsSaving ? "Saving widget…" : Dirty ? "Unsaved edits" : "Saved";

    public SkinDefinition CaptureRecoveryDraft() => _working.Clone();
    private string _recoveryStatus = "Recovery checkpoints start when you edit.";
    public string RecoveryStatus { get => _recoveryStatus; set => SetProperty(ref _recoveryStatus, value); }

    public bool SnapSelectedMeters(IEnumerable<MeterEditorItem> selection, double gridSize = 10)
    {
        var items = selection.Distinct().ToArray();
        if (!double.IsFinite(gridSize) || gridSize <= 0 || items.Length == 0 || items.Any(m => !Meters.Contains(m))) return false;
        if (items.Any(m => ParentOf(m) != ParentOf(items[0])))
        { StatusMessage = "Choose meters with the same parent before snapping."; return false; }
        var positions = items.Select(m => (Meter: m, X: Math.Round(m.X / gridSize) * gridSize, Y: Math.Round(m.Y / gridSize) * gridSize)).ToArray();
        if (positions.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return false;
        if (positions.All(p => p.Meter.X == p.X && p.Meter.Y == p.Y)) return false;
        using var edit = BeginEdit();
        foreach (var p in positions) { p.Meter.X = p.X; p.Meter.Y = p.Y; }
        StatusMessage = "Snapped selected meters to the grid. Preview only until Save.";
        return true;
    }
}
