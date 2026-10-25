using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class DesktopScenePersistenceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ThemedAI_Scenes_" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("add", false)]
    [InlineData("replace", false)]
    [InlineData("delete", false)]
    [InlineData("add", true)]
    [InlineData("replace", true)]
    [InlineData("delete", true)]
    public async Task FailedMutationPreservesInventoryAndActiveScene(string operation, bool cancel)
    {
        var repository = new DesktopSceneRepository(_folder);
        var original = new DesktopScene { Id = "original", Name = "Saved world" };
        await repository.SaveAllAsync(new[] { original });
        await repository.SaveActiveSceneIdAsync(original.Id);
        var service = new DesktopSceneService(repository);
        await service.InitializeAsync();
        var saved = Assert.Single(service.Scenes);
        var sceneEvents = 0;
        var activeEvents = 0;
        service.ScenesChanged += (_, _) => sceneEvents++;
        service.ActiveSceneChanged += (_, _) => activeEvents++;
        using var cancellation = new CancellationTokenSource();
        if (cancel) cancellation.Cancel();
        // A directory at the temporary-file path deterministically rejects File.Create.
        else Directory.CreateDirectory(Path.Combine(_folder, "scenes.json.tmp"));

        var candidate = new DesktopScene { Id = operation == "add" ? "new" : "original", Name = "Unsaved world" };
        var beforeModified = candidate.LastModified;
        var error = await Record.ExceptionAsync(() => operation == "delete"
            ? service.DeleteAsync(saved.Id, cancellation.Token)
            : service.UpsertAsync(candidate, cancellation.Token));
        Assert.NotNull(error);
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
        else Assert.True(error is IOException or UnauthorizedAccessException, error.ToString());

        Assert.Same(saved, Assert.Single(service.Scenes));
        Assert.Same(saved, service.ActiveScene);
        Assert.Equal("Saved world", Assert.Single(await repository.LoadAllAsync()).Name);
        Assert.Equal(saved.Id, await repository.LoadActiveSceneIdAsync());
        Assert.Equal(0, sceneEvents);
        Assert.Equal(0, activeEvents);
        if (operation != "delete") Assert.Equal(beforeModified, candidate.LastModified);
    }

    [Fact]
    public async Task ConcurrentAddsPersistEveryWorldAndNotifyAfterCommit()
    {
        var repository = new DesktopSceneRepository(_folder);
        var service = new DesktopSceneService(repository);
        await service.InitializeAsync();
        var notifications = 0;
        service.ScenesChanged += (_, _) => notifications++;
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            service.UpsertAsync(new DesktopScene { Id = "world-" + i })));
        var stored = await repository.LoadAllAsync();
        Assert.Equal(20, stored.Count);
        Assert.Equal(20, service.Scenes.Count);
        Assert.Equal(20, notifications);
        Assert.Equal(service.Scenes.Select(s => s.Id).OrderBy(id => id), stored.Select(s => s.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task SuccessfulDeleteRemovesWorldBeforeNotifying()
    {
        var repository = new DesktopSceneRepository(_folder);
        var service = new DesktopSceneService(repository);
        await service.UpsertAsync(new DesktopScene { Id = "remove" });
        var notifications = 0;
        service.ScenesChanged += (_, _) => { Assert.Empty(service.Scenes); notifications++; };
        await service.DeleteAsync("remove");
        Assert.Empty(await repository.LoadAllAsync());
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverlappingActiveWritesPersistLastRequest(bool clearLast)
    {
        var repository = new DesktopSceneRepository(_folder);
        // A large value keeps serialization asynchronous and exposes overlapping
        // access to the shared temporary file without sleeps or timing assertions.
        var writes = new List<Task>();
        for (var i = 0; i < 20; i++)
            writes.Add(repository.SaveActiveSceneIdAsync(i + new string('x', 65536)));
        writes.Add(repository.SaveActiveSceneIdAsync(null));
        var expected = clearLast ? null : "last-world";
        writes.Add(repository.SaveActiveSceneIdAsync(expected));
        await Task.WhenAll(writes);
        Assert.Equal(expected, await repository.LoadActiveSceneIdAsync());
    }

    [Fact]
    public async Task CancelledActiveClearRetainsSavedIdentity()
    {
        var repository = new DesktopSceneRepository(_folder);
        await repository.SaveActiveSceneIdAsync("keep");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.SaveActiveSceneIdAsync(null, cancellation.Token));
        Assert.Equal("keep", await repository.LoadActiveSceneIdAsync());
    }

    [Fact]
    public async Task ActiveWriteFailureDoesNotBlockLaterWrite()
    {
        var repository = new DesktopSceneRepository(_folder);
        await repository.SaveActiveSceneIdAsync("keep");
        var temp = Path.Combine(_folder, "active-scene.json.tmp");
        Directory.CreateDirectory(temp);
        var error = await Record.ExceptionAsync(() => repository.SaveActiveSceneIdAsync("failed"));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("keep", await repository.LoadActiveSceneIdAsync());
        Directory.Delete(temp);
        await repository.SaveActiveSceneIdAsync("retry").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("retry", await repository.LoadActiveSceneIdAsync());
    }
    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}