using ThemeManager.Core.Models;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// Bounded undo/redo stack for palette edits in the theme editor.
///
/// Each "snapshot" is a lightweight struct capturing only the 8 hex tokens
/// plus the scale values — the minimum state needed to fully undo/redo a change.
///
/// Python analogy: a deque-based command history, similar to what you'd build
/// with collections.deque(maxlen=50) for a paint application.
/// </summary>
public sealed class PaletteHistory
{
    private const int MaxSteps = 50;

    private readonly LinkedList<PaletteSnapshot> _undoStack = new();
    private readonly LinkedList<PaletteSnapshot> _redoStack = new();

    // Raised whenever the stack state changes so the ViewModel can update
    // CanUndo / CanRedo without polling.
    public event EventHandler? HistoryChanged;

    // ── State ─────────────────────────────────────────────────────────────────
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int  UndoDepth => _undoStack.Count;

    // ── Record a change ───────────────────────────────────────────────────────

    /// <summary>
    /// Call this BEFORE applying a palette mutation to capture the previous state.
    /// Clears the redo stack (standard undo/redo semantics).
    /// </summary>
    public void Push(CozyTheme theme)
    {
        _undoStack.AddLast(PaletteSnapshot.From(theme));
        if (_undoStack.Count > MaxSteps)
            _undoStack.RemoveFirst();

        _redoStack.Clear(); // branching edit wipes the redo future
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Undo ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores the previous palette state.
    /// <paramref name="currentTheme"/> is the theme to push onto the redo stack.
    /// Returns the snapshot to apply, or null if nothing to undo.
    /// </summary>
    public PaletteSnapshot? Undo(CozyTheme currentTheme)
    {
        if (!CanUndo) return null;

        _redoStack.AddLast(PaletteSnapshot.From(currentTheme));
        var snap = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return snap;
    }

    // ── Redo ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-applies the next palette state after an undo.
    /// Returns the snapshot to apply, or null if nothing to redo.
    /// </summary>
    public PaletteSnapshot? Redo(CozyTheme currentTheme)
    {
        if (!CanRedo) return null;

        _undoStack.AddLast(PaletteSnapshot.From(currentTheme));
        var snap = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return snap;
    }

    /// <summary>Wipes both stacks (called when a new theme is loaded).</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}

// ── Snapshot ──────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight immutable copy of the palette tokens at one point in time.
/// Captured before every edit so undo is always available.
/// </summary>
public sealed record PaletteSnapshot(
    string BackgroundBase,
    string BackgroundAlt,
    string Surface,
    string AccentPrimary,
    string AccentStrong,
    string TextPrimary,
    string TextMuted,
    string BorderSubtle,
    double CornerRadiusScale,
    double DensityScale
)
{
    public static PaletteSnapshot From(CozyTheme t) => new(
        t.BackgroundBase, t.BackgroundAlt, t.Surface,
        t.AccentPrimary,  t.AccentStrong,
        t.TextPrimary,    t.TextMuted,    t.BorderSubtle,
        t.CornerRadiusScale, t.DensityScale);

    /// <summary>Applies this snapshot back to a live theme (for undo/redo restore).</summary>
    public void ApplyTo(CozyTheme t)
    {
        t.BackgroundBase     = BackgroundBase;
        t.BackgroundAlt      = BackgroundAlt;
        t.Surface            = Surface;
        t.AccentPrimary      = AccentPrimary;
        t.AccentStrong       = AccentStrong;
        t.TextPrimary        = TextPrimary;
        t.TextMuted          = TextMuted;
        t.BorderSubtle       = BorderSubtle;
        t.CornerRadiusScale  = CornerRadiusScale;
        t.DensityScale       = DensityScale;
    }
}
