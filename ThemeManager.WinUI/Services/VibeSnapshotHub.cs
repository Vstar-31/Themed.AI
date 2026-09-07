namespace ThemeManager.WinUI.Services;

/// <summary>
/// Process-wide bridge for VibeFinder/media providers and the desktop world runtime.
/// Providers publish snapshots; scenes can subscribe without knowing where the data came from.
/// </summary>
public static class VibeSnapshotHub
{
    private static readonly object Gate = new();
    private static VibeSnapshot _current = VibeSnapshot.Neutral;

    public static VibeSnapshot Current
    {
        get { lock (Gate) return _current; }
    }

    public static event EventHandler<VibeSnapshot>? SnapshotChanged;

    public static void Publish(VibeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (Gate) _current = snapshot;
        SnapshotChanged?.Invoke(null, snapshot);
    }
}
