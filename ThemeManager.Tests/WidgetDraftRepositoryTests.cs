using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

public sealed class WidgetDraftRepositoryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "themed-drafts-" + Guid.NewGuid().ToString("N"));
    private WidgetDraftRepository Repository => new(_folder);
    private static SkinDefinition Draft(string name = "Unfinished") => new() { Name = name, Meters = new() { new() { Id = "text", StaticText = "Keep this", X = 17 } } };
    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }

    [Fact] public async Task SurvivesRepositoryRestartWithoutMutatingSource()
    {
        var id = Guid.NewGuid(); var source = Draft();
        await Repository.SaveAsync(id, source);
        source.Meters[0].StaticText = "Changed after save";
        var (drafts, errors) = await new WidgetDraftRepository(_folder).ListAsync();
        Assert.Equal(0, errors); var saved = Assert.Single(drafts);
        Assert.Equal(id, saved.Id); Assert.Equal("Keep this", saved.Definition.Meters[0].StaticText);
        Assert.Equal(source.Id, saved.Definition.Id);
    }
    [Fact] public async Task IndependentSessionsRemainAvailable()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await Repository.SaveAsync(a, Draft("First")); await Repository.SaveAsync(b, Draft("Second"));
        await Repository.DeleteAsync(a);
        Assert.Equal(b, Assert.Single((await Repository.ListAsync()).Drafts).Id);
        await Repository.DeleteAsync(a); // idempotent
    }
    [Fact] public async Task FailedReplacementKeepsLastGoodCheckpointAndCanRetry()
    {
        var id = Guid.NewGuid(); await Repository.SaveAsync(id, Draft("Original"));
        var blocked = Path.Combine(_folder, id.ToString("N") + ".json.tmp"); Directory.CreateDirectory(blocked);
        await Assert.ThrowsAnyAsync<Exception>(() => Repository.SaveAsync(id, Draft("New")));
        Assert.Equal("Original", Assert.Single((await Repository.ListAsync()).Drafts).Definition.Name);
        Directory.Delete(blocked); await Repository.SaveAsync(id, Draft("Retry"));
        Assert.Equal("Retry", Assert.Single((await Repository.ListAsync()).Drafts).Definition.Name);
    }
    [Fact] public async Task CorruptionDoesNotHideHealthyDraftsOrDeleteEvidence()
    {
        await Repository.SaveAsync(Guid.NewGuid(), Draft());
        var bad = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".json"); await File.WriteAllTextAsync(bad, "{broken");
        var (drafts, errors) = await Repository.ListAsync(); Assert.Single(drafts); Assert.Equal(1, errors); Assert.True(File.Exists(bad));
    }
    [Fact] public async Task FilenameIdentityMismatchIsNotRecoverable()
    {
        var id = Guid.NewGuid(); await Repository.SaveAsync(id, Draft());
        File.Move(Path.Combine(_folder, id.ToString("N") + ".json"), Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".json"));
        var (drafts, errors) = await Repository.ListAsync(); Assert.Empty(drafts); Assert.Equal(1, errors);
    }
    [Fact] public async Task OverlappingWritesProduceCompleteLatestCheckpoint()
    {
        var id = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Repository.SaveAsync(id, Draft(new string((char)('a' + i), 10000)))));
        var saved = Assert.Single((await Repository.ListAsync()).Drafts);
        Assert.Equal(new string('t', 10000), saved.Definition.Name);
    }
    [Fact] public async Task DeleteAfterQueuedWriteDoesNotResurrectDraft()
    {
        var id = Guid.NewGuid(); var save = Repository.SaveAsync(id, Draft()); var delete = Repository.DeleteAsync(id);
        await Task.WhenAll(save, delete); Assert.Empty((await Repository.ListAsync()).Drafts);
    }
    [Fact] public async Task OversizedCheckpointPreservesPreviousFile()
    {
        var id = Guid.NewGuid(); await Repository.SaveAsync(id, Draft("Original"));
        await Assert.ThrowsAsync<IOException>(() => Repository.SaveAsync(id, Draft(new string('x', 17 * 1024 * 1024))));
        Assert.Equal("Original", Assert.Single((await Repository.ListAsync()).Drafts).Definition.Name);
    }
    [Fact] public async Task RecoveryIsDisabledCopyWithNewIdentity()
    {
        var source = Draft(); source.Enabled = true; await Repository.SaveAsync(Guid.NewGuid(), source);
        var recovered = WidgetRemix.Create(Assert.Single((await Repository.ListAsync()).Drafts).Definition);
        Assert.NotEqual(source.Id, recovered.Id); Assert.False(recovered.Enabled);
        Assert.Equal("Keep this", recovered.Meters[0].StaticText);
    }
}
