using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

public sealed class DesktopFeedbackRegressionTests
{
    [Fact]
    public void ColorDrag_UndoesToStartAndRedoesToFinalColor()
    {
        var history = new PaletteHistory();
        var theme = new CozyTheme { AccentPrimary = "#000000" };
        for (int i = 1; i <= 100; i++)
        {
            history.PushContinuous(theme, "AccentPrimary", i * 10);
            theme.AccentPrimary = $"#{i:X2}0000";
        }
        Assert.Equal(1, history.UndoDepth);
        history.Undo(theme)!.ApplyTo(theme);
        Assert.Equal("#000000", theme.AccentPrimary);
        history.Redo(theme)!.ApplyTo(theme);
        Assert.Equal("#640000", theme.AccentPrimary);
    }

    [Fact]
    public void ColorEdits_PauseControlChangeAndReleaseCreateSeparateSteps()
    {
        var history = new PaletteHistory();
        var theme = new CozyTheme();
        history.PushContinuous(theme, "accent", 0);
        history.PushContinuous(theme, "accent", 100);
        history.PushContinuous(theme, "accent", 550);
        history.PushContinuous(theme, "background", 560);
        history.EndContinuousEdit();
        history.PushContinuous(theme, "background", 570);
        Assert.Equal(4, history.UndoDepth);
    }

    [Fact]
    public void UndoThenImmediateEdit_DoesNotJoinOldGestureAndClearsRedo()
    {
        var history = new PaletteHistory();
        var theme = new CozyTheme { AccentPrimary = "#000000" };
        history.PushContinuous(theme, "accent", 0);
        theme.AccentPrimary = "#FFFFFF";
        history.Undo(theme)!.ApplyTo(theme);
        history.PushContinuous(theme, "accent", 10);
        theme.AccentPrimary = "#FF0000";
        Assert.False(history.CanRedo);
        Assert.Equal(1, history.UndoDepth);
        history.Undo(theme)!.ApplyTo(theme);
        Assert.Equal("#000000", theme.AccentPrimary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateSavedWidgetIds_AreRepairedWithoutLosingDistinctEdits(bool different)
    {
        var folder = Path.Combine(Path.GetTempPath(), "ThemedAI_feedback_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new SkinRepository(folder);
            var first = new SkinDefinition { Id = "duplicate", Name = "VibeFinder Playlist", Enabled = true };
            var second = first.Clone("DUPLICATE");
            if (different) second.Name = "My custom version";
            await repo.SaveAllCheckedAsync(new[] { first, second });
            var loaded = await repo.LoadAllAsync();
            Assert.Equal(different ? 2 : 1, loaded.Count);
            Assert.Equal(loaded.Count, loaded.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal("VibeFinder Playlist", loaded[0].Name);
            if (different)
            {
                Assert.Contains("My custom version", loaded[1].Name);
                Assert.False(loaded[1].Enabled);
            }
            var again = await repo.LoadAllAsync();
            Assert.Equal(loaded.Select(s => s.Id), again.Select(s => s.Id));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
