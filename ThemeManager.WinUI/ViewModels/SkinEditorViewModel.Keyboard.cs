using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed partial class SkinEditorViewModel
{
    private bool _canRecoverAsCopy;
    public bool CanRecoverAsCopy
    {
        get => _canRecoverAsCopy;
        private set => SetProperty(ref _canRecoverAsCopy, value);
    }

    public bool RecoverAsNewDraft(IEnumerable<string> existingNames)
    {
        if (!CanRecoverAsCopy) return false;
        var copy = WidgetRemix.Create(_working, existingNames);
        LoadSkin(copy, newDraft: true);
        StatusMessage = "Your edits are now a new draft. Choose Save to create and enable it.";
        return true;
    }

    public bool NudgeLayer(string key, double dx, double dy)
    {
        if (key.StartsWith("meter:", StringComparison.Ordinal))
        {
            var meter = Meters.FirstOrDefault(m => m.Definition.Id == key[6..]);
            return meter is not null && NudgeMeters(new[] { meter }, dx, dy);
        }
        if (!key.StartsWith("group:", StringComparison.Ordinal)) return false;
        var group = Groups.FirstOrDefault(g => g.Definition.Id == key[6..]);
        if (group is null || !CanMove(group.X, group.Y, dx, dy)) return false;
        using var edit = BeginEdit();
        group.X += dx;
        group.Y += dy;
        StatusMessage = "Moved the group. Preview only until Save.";
        return true;
    }

    public bool NudgeMeters(IEnumerable<MeterEditorItem> selection, double dx, double dy)
    {
        var meters = selection.Distinct().ToArray();
        // Reject stale selections rather than moving only part of the intended selection.
        if (meters.Length == 0 || meters.Any(m => !Meters.Contains(m))) return false;
        if (meters.Any(m => ParentOf(m) != ParentOf(meters[0])))
        {
            StatusMessage = "Choose meters in the same group, or choose only ungrouped meters.";
            return false;
        }
        if (meters.Any(m => !CanMove(m.X, m.Y, dx, dy))) return false;
        using var edit = BeginEdit();
        foreach (var meter in meters) { meter.X += dx; meter.Y += dy; }
        StatusMessage = "Moved selected meters in their local coordinates. Preview only until Save.";
        return true;
    }

    private bool CanMove(double x, double y, double dx, double dy)
    {
        if (dx == 0 && dy == 0) return false;
        if (double.IsFinite(dx) && double.IsFinite(dy) && double.IsFinite(x + dx) && double.IsFinite(y + dy))
            return true;
        StatusMessage = "These coordinates are too large to move. Reduce their values first.";
        return false;
    }
}
