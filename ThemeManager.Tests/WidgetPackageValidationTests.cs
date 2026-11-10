using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetPackageValidationTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validator_RejectsNonFiniteGeometry(double value)
    {
        var skin = new SkinDefinition
        {
            Width = value, X = value,
            Meters = new() { new() { Rotation = value, Opacity = value } },
            Groups = new() { new() { ScaleX = value } },
        };
        var issues = WidgetDefinitionValidator.Validate(skin);
        Assert.Contains(issues, i => i.Path == "size");
        Assert.Contains(issues, i => i.Path == "position");
        Assert.Contains(issues, i => i.Path.EndsWith(".opacity"));
        Assert.Contains(issues, i => i.Path.EndsWith(".scale"));
    }

    [Fact]
    public void Validator_RejectsAmbiguousMeterAndGroupIds()
    {
        var skin = new SkinDefinition
        {
            Meters = new() { new() { Id = "same" }, new() { Id = "SAME" } },
            Groups = new() { new() { Id = "group" }, new() { Id = "GROUP" } },
        };
        var issues = WidgetDefinitionValidator.Validate(skin);
        Assert.Contains(issues, i => i.Path == "meters.id");
        Assert.Contains(issues, i => i.Path == "groups.id");
    }

    [Fact]
    public void Clone_PreservesImageWithoutSharingMeterState()
    {
        var skin = new SkinDefinition { Meters = new() { new() { Kind = MeterKind.Image, ImagePath = "example.png" } } };
        var copy = skin.Clone();
        Assert.Equal("example.png", copy.Meters[0].ImagePath);
        copy.Meters[0].ImagePath = "changed.png";
        Assert.Equal("example.png", skin.Meters[0].ImagePath);
    }

    [Fact]
    public async Task CheckedSave_ReportsFailureWhileExistingBestEffortSaveRemainsRecoverable()
    {
        var blocker = Path.GetTempFileName();
        try
        {
            var repository = new SkinRepository(blocker);
            await Assert.ThrowsAsync<IOException>(() => repository.SaveAllCheckedAsync(new[] { new SkinDefinition() }));
            await repository.SaveAllAsync(new[] { new SkinDefinition() });
            Assert.True(File.Exists(blocker));
        }
        finally { File.Delete(blocker); }
    }

    [Fact]
    public async Task CheckedSave_PersistsImportedImagesAndGroupsAcrossRestart()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ThemedAI-storage", Guid.NewGuid().ToString("N"));
        try
        {
            var skin = new SkinDefinition
            {
                Enabled = false,
                Meters = new() { new() { Kind = MeterKind.Image, ImagePath = "installed.png" } },
            };
            skin.Groups.Add(new WidgetGroupDefinition { MeterIds = new() { skin.Meters[0].Id } });
            await new SkinRepository(folder).SaveAllCheckedAsync(new[] { skin });
            var loaded = Assert.Single(await new SkinRepository(folder).LoadAllAsync());
            Assert.False(loaded.Enabled);
            Assert.Equal("installed.png", loaded.Meters[0].ImagePath);
            Assert.Equal(loaded.Meters[0].Id, Assert.Single(loaded.Groups[0].MeterIds));
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
