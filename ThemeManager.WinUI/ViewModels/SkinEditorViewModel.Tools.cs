using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed record SkinEditorDraft(SkinDefinition Definition, Guid? RecoveryId = null);

public sealed partial class SkinEditorViewModel
{
    // Store immutable draft data only, never live controls or running widget instances.
    private static readonly JsonSerializerOptions HistoryJson = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private readonly List<string> _history = new();
    private int _historyPosition;
    private int _editDepth;
    private string _savedSnapshot = "";
    private bool _newDraft;
    public bool CanDelete => !_newDraft;
    public bool IsRestoringDraft { get; private set; }
    public bool CanUndo => _editDepth == 0 && _historyPosition > 0;
    public bool CanRedo => _editDepth == 0 && _historyPosition < _history.Count - 1;
    public string HistoryStatus => $"{_historyPosition} undo · {Math.Max(0, _history.Count - _historyPosition - 1)} redo";
    public event Action? DraftRestored;

    private string Snapshot() => JsonSerializer.Serialize(_working, HistoryJson);

    public void LoadSkin(SkinDefinition skin, bool newDraft = false)
    {
        _newDraft = newDraft;
        CanRecoverAsCopy = false;
        IsRestoringDraft = true;
        try { ApplyDraft(skin); }
        finally { IsRestoringDraft = false; }
        _editDepth = 0;
        _history.Clear();
        _savedSnapshot = Snapshot();
        _history.Add(_savedSnapshot);
        _historyPosition = 0;
        Dirty = _newDraft;
        OnPropertyChanged(nameof(CanDelete));
        NotifyHistory();
        DraftRestored?.Invoke();
    }

    private void RecordHistory()
    {
        if (IsRestoringDraft || _editDepth > 0 || _history.Count == 0) return;
        var snapshot = Snapshot();
        if (snapshot == _history[_historyPosition]) return;
        _history.RemoveRange(_historyPosition + 1, _history.Count - _historyPosition - 1);
        _history.Add(snapshot);
        // Bound both count and UTF-16 payload. Keep the current draft even if it is oversized.
        long size = _history.Sum(s => (long)s.Length * sizeof(char));
        while (_history.Count > 1 && (_history.Count > 101 || size > 8 * 1024 * 1024))
        {
            size -= (long)_history[0].Length * sizeof(char);
            _history.RemoveAt(0);
        }
        _historyPosition = _history.Count - 1;
        NotifyHistory();
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(HistoryStatus));
    }

    public IDisposable BeginEdit()
    {
        _editDepth++;
        NotifyHistory();
        return new EditScope(this);
    }

    private sealed class EditScope(SkinEditorViewModel owner) : IDisposable
    {
        private SkinEditorViewModel? _owner = owner;
        public void Dispose()
        {
            if (_owner is not { } owner) return;
            _owner = null;
            if (--owner._editDepth != 0) return;
            owner.RecordHistory();
            owner.Dirty = owner._newDraft || owner.Snapshot() != owner._savedSnapshot;
            owner.NotifyHistory();
        }
    }

    public void Undo() { if (CanUndo) RestoreHistory(_historyPosition - 1, "Undid the last edit."); }
    public void Redo() { if (CanRedo) RestoreHistory(_historyPosition + 1, "Redid the last edit."); }

    private void RestoreHistory(int position, string message)
    {
        var draft = JsonSerializer.Deserialize<SkinDefinition>(_history[position], HistoryJson)!;
        var meterId = SelectedMeter?.Definition.Id;
        var groupId = SelectedGroup?.Definition.Id;
        IsRestoringDraft = true;
        try
        {
            ApplyDraft(draft);
            SelectedMeter = meterId is null && Groups.Any(g => g.Definition.Id == groupId)
                ? null : Meters.FirstOrDefault(m => m.Definition.Id == meterId) ?? Meters.FirstOrDefault();
            SelectedGroup = Groups.FirstOrDefault(g => g.Definition.Id == groupId) ?? Groups.FirstOrDefault();
            _historyPosition = position;
            Dirty = _newDraft || Snapshot() != _savedSnapshot;
            StatusMessage = message + (Dirty ? " Preview only until Save." : " Back to the saved draft.");
        }
        finally { IsRestoringDraft = false; }
        NotifyHistory();
        DraftRestored?.Invoke();
    }

    private GroupEditorItem? ParentOf(MeterEditorItem meter) => Groups.FirstOrDefault(g =>
        g.Definition.MeterIds.Contains(meter.Definition.Id, StringComparer.OrdinalIgnoreCase));

    public void ArrangeMeter(string action)
    {
        if (SelectedMeter is { } meter) ArrangeLayer("meter:" + meter.Definition.Id, action);
    }

    public void AlignMeters(IEnumerable<MeterEditorItem> selection, string action)
    {
        var items = selection.Where(Meters.Contains).Distinct().ToList();
        bool distribute = action is "DistributeX" or "DistributeY";
        if (items.Count < (distribute ? 3 : 2))
        {
            StatusMessage = distribute ? "Select at least three meters to distribute." : "Select at least two meters to align.";
            return;
        }
        if (items.Any(m => ParentOf(m) != ParentOf(items[0])))
        {
            StatusMessage = "Choose meters in the same group, or choose only ungrouped meters.";
            return;
        }
        double left = items.Min(m => m.X), top = items.Min(m => m.Y);
        double right = items.Max(m => m.X + m.Width), bottom = items.Max(m => m.Y + m.Height);
        var positions = new List<(MeterEditorItem Meter, double X, double Y)>();
        if (distribute)
        {
            bool horizontal = action == "DistributeX";
            var sorted = items.OrderBy(m => horizontal ? m.X : m.Y).ThenBy(m => m.Definition.Id).ToList();
            // Preserve the outer layout bounds and provide equal gaps between all selected meters.
            double span = horizontal ? right - left : bottom - top;
            double gap = (span - sorted.Sum(m => horizontal ? m.Width : m.Height)) / (sorted.Count - 1);
            if (gap < 0) { StatusMessage = "Spread the outer meters farther apart to distribute without overlap."; return; }
            double cursor = horizontal ? left : top;
            foreach (var meter in sorted)
            {
                positions.Add((meter, horizontal ? cursor : meter.X, horizontal ? meter.Y : cursor));
                cursor += (horizontal ? meter.Width : meter.Height) + gap;
            }
        }
        else
        {
            if (action is not ("Left" or "Right" or "CenterX" or "Top" or "Bottom" or "CenterY")) return;
            foreach (var meter in items)
                positions.Add((meter,
                    action switch { "Left" => left, "Right" => right - meter.Width, "CenterX" => (left + right - meter.Width) / 2, _ => meter.X },
                    action switch { "Top" => top, "Bottom" => bottom - meter.Height, "CenterY" => (top + bottom - meter.Height) / 2, _ => meter.Y }));
        }
        if (positions.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
        {
            StatusMessage = "These coordinates are too large to align. Reduce their values first.";
            return;
        }
        using var edit = BeginEdit();
        foreach (var (meter, x, y) in positions) { meter.X = x; meter.Y = y; }
        StatusMessage = distribute ? "Distributed selected meters with equal gaps." : "Aligned selected meter layout bounds.";
    }
}
