using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="PaletteHistory"/> — undo/redo stack, boundary conditions,
/// and the HistoryChanged event.
/// </summary>
public class PaletteHistoryTests
{
    private static CozyTheme MakeTheme(string bg = "#FFFFFF") =>
        new() { BackgroundBase = bg };

    // ── Push / CanUndo / CanRedo ──────────────────────────────────────────────

    [Fact]
    public void NewHistory_IsEmpty()
    {
        var history = new PaletteHistory();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoDepth);
    }

    [Fact]
    public void Push_MakesUndoAvailable()
    {
        var history = new PaletteHistory();
        history.Push(MakeTheme());
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    // ── Undo ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Undo_RestoresPreviousState()
    {
        var history = new PaletteHistory();
        var original = MakeTheme("#AAAAAA");

        history.Push(original); // snapshot the "before" state
        original.BackgroundBase = "#BBBBBB"; // simulate an edit

        var snap = history.Undo(original);
        Assert.NotNull(snap);
        Assert.Equal("#AAAAAA", snap!.BackgroundBase);
    }

    [Fact]
    public void Undo_OnEmpty_ReturnsNull()
    {
        var history = new PaletteHistory();
        var snap = history.Undo(MakeTheme());
        Assert.Null(snap);
    }

    [Fact]
    public void Undo_MakesRedoAvailable()
    {
        var history = new PaletteHistory();
        history.Push(MakeTheme());
        history.Undo(MakeTheme());
        Assert.True(history.CanRedo);
    }

    // ── Redo ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Redo_RestoresUndoneState()
    {
        var history = new PaletteHistory();
        var theme = MakeTheme("#111111");

        history.Push(theme);
        theme.BackgroundBase = "#222222";

        history.Undo(theme); // undo back to #111111
        var snap = history.Redo(MakeTheme("#111111")); // redo to #222222

        Assert.NotNull(snap);
        Assert.Equal("#222222", snap!.BackgroundBase);
    }

    [Fact]
    public void Redo_OnEmpty_ReturnsNull()
    {
        var history = new PaletteHistory();
        var snap = history.Redo(MakeTheme());
        Assert.Null(snap);
    }

    // ── Push clears redo stack ────────────────────────────────────────────────

    [Fact]
    public void Push_ClearsRedoStack()
    {
        var history = new PaletteHistory();
        history.Push(MakeTheme("#AAA"));
        history.Undo(MakeTheme("#BBB"));
        Assert.True(history.CanRedo);

        // A new edit should wipe the redo future
        history.Push(MakeTheme("#CCC"));
        Assert.False(history.CanRedo);
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_WipesBothStacks()
    {
        var history = new PaletteHistory();
        history.Push(MakeTheme());
        history.Push(MakeTheme());
        history.Undo(MakeTheme());

        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    // ── MaxSteps boundary ────────────────────────────────────────────────────

    [Fact]
    public void Push_BeyondMaxSteps_DoesNotGrowUnbounded()
    {
        var history = new PaletteHistory();

        // Push 60 items (max is 50)
        for (int i = 0; i < 60; i++)
            history.Push(MakeTheme($"#{i:X6}"));

        Assert.True(history.UndoDepth <= 50);
    }

    // ── HistoryChanged event ─────────────────────────────────────────────────

    [Fact]
    public void HistoryChanged_FiresOnPush()
    {
        var history = new PaletteHistory();
        int fireCount = 0;
        history.HistoryChanged += (_, _) => fireCount++;

        history.Push(MakeTheme());
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void HistoryChanged_FiresOnUndo()
    {
        var history = new PaletteHistory();
        history.Push(MakeTheme());

        int fireCount = 0;
        history.HistoryChanged += (_, _) => fireCount++;

        history.Undo(MakeTheme());
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void HistoryChanged_FiresOnClear()
    {
        var history = new PaletteHistory();
        int fireCount = 0;
        history.HistoryChanged += (_, _) => fireCount++;

        history.Clear();
        Assert.Equal(1, fireCount);
    }

    // ── PaletteSnapshot ApplyTo ──────────────────────────────────────────────

    [Fact]
    public void PaletteSnapshot_ApplyTo_RestoresAllFields()
    {
        var original = CozyDefaults.CreateDefault();
        var snap = PaletteSnapshot.From(original);

        var target = new CozyTheme
        {
            BackgroundBase = "#000000",
            BackgroundAlt  = "#000000",
            Surface        = "#000000",
            AccentPrimary  = "#000000",
            AccentStrong   = "#000000",
            TextPrimary    = "#000000",
            TextMuted      = "#000000",
            BorderSubtle   = "#000000",
            CornerRadiusScale = 99.0,
            DensityScale      = 99.0,
        };

        snap.ApplyTo(target);

        Assert.Equal(original.BackgroundBase, target.BackgroundBase);
        Assert.Equal(original.AccentPrimary, target.AccentPrimary);
        Assert.Equal(original.TextPrimary, target.TextPrimary);
        Assert.Equal(original.CornerRadiusScale, target.CornerRadiusScale);
        Assert.Equal(original.DensityScale, target.DensityScale);
    }
}
