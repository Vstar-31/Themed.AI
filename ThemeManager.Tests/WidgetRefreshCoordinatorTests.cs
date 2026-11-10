using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetRefreshCoordinatorTests
{
    [Fact]
    public async Task SlowMeasureAndPendingUiUpdateBothSuppressFurtherTicks()
    {
        var coordinator = new WidgetRefreshCoordinator();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        int refreshes = 0, updates = 0;
        bool Enqueue(Action action) { queued.TrySetResult(action); return true; }
        var first = coordinator.TryRefreshAsync(() =>
        {
            Interlocked.Increment(ref refreshes);
            started.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
        }, Enqueue, () => updates++, () => false);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (var tick = 0; tick < 100; tick++)
                Assert.False(await coordinator.TryRefreshAsync(() => refreshes++, Enqueue, () => updates++, () => false));
        }
        finally { release.Set(); }
        var callback = await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await coordinator.TryRefreshAsync(() => refreshes++, Enqueue, () => updates++, () => false));
        callback();
        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, refreshes);
        Assert.Equal(1, updates);
        Assert.True(await coordinator.TryRefreshAsync(() => refreshes++, a => { a(); return true; }, () => updates++, () => false));
        Assert.Equal(2, updates);
    }

    [Fact]
    public async Task ClosingBeforeUiUpdateDiscardsStaleWorkWithoutBlockingReplacementInstance()
    {
        var old = new WidgetRefreshCoordinator();
        var replacement = new WidgetRefreshCoordinator();
        var queued = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool closed = false;
        int updates = 0;
        var pending = old.TryRefreshAsync(() => { }, a => { queued.SetResult(a); return true; }, () => updates++, () => Volatile.Read(ref closed));
        var callback = await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Write(ref closed, true);
        Assert.True(await replacement.TryRefreshAsync(() => { }, a => { a(); return true; }, () => updates++, () => false));
        callback();
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, updates);
    }

    [Fact]
    public async Task DispatcherRejectionAndExceptionsReleaseTheNextTick()
    {
        var coordinator = new WidgetRefreshCoordinator();
        Assert.False(await coordinator.TryRefreshAsync(() => { }, _ => false, () => Assert.Fail("Rejected update ran"), () => false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.TryRefreshAsync(
            () => throw new InvalidOperationException(), a => { a(); return true; }, () => { }, () => false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.TryRefreshAsync(
            () => { }, a => { a(); return true; }, () => throw new InvalidOperationException(), () => false));
        Assert.True(await coordinator.TryRefreshAsync(() => { }, a => { a(); return true; }, () => { }, () => false));
    }
}
