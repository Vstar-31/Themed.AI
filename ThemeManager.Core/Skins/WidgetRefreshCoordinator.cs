namespace ThemeManager.Core.Skins;

/// <summary>Allows one refresh, including its pending UI update, per widget instance.</summary>
public sealed class WidgetRefreshCoordinator
{
    private int _busy;

    public async Task<bool> TryRefreshAsync(
        Action refresh, Func<Action, bool> enqueue, Action update, Func<bool> isClosed)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return false;
        try
        {
            if (isClosed()) return false;
            await Task.Run(() => { if (!isClosed()) refresh(); }).ConfigureAwait(false);
            if (isClosed()) return false;

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!enqueue(() =>
            {
                try
                {
                    if (isClosed()) { completion.TrySetResult(false); return; }
                    update();
                    completion.TrySetResult(true);
                }
                catch (Exception ex) { completion.TrySetException(ex); }
            })) return false;

            return await completion.Task.ConfigureAwait(false);
        }
        finally { Volatile.Write(ref _busy, 0); }
    }
}
