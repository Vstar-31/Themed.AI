using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

public sealed class DesktopSceneService
{
    private readonly DesktopSceneRepository _repository;
    private List<DesktopScene> _scenes = new();
    private bool _initialized;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public IReadOnlyList<DesktopScene> Scenes => _scenes;
    public DesktopScene? ActiveScene { get; private set; }
    public event EventHandler<DesktopScene?>? ActiveSceneChanged;
    public event EventHandler? ScenesChanged;

    public DesktopSceneService(DesktopSceneRepository repository) => _repository = repository;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _initializeGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            _scenes = await _repository.LoadAllAsync(cancellationToken);

            var activeId = await _repository.LoadActiveSceneIdAsync(cancellationToken);
            var restored = !string.IsNullOrWhiteSpace(activeId)
                ? _scenes.FirstOrDefault(s => s.Id.Equals(activeId, StringComparison.OrdinalIgnoreCase))
                : null;

            ActiveScene = restored ?? _scenes.FirstOrDefault();
            _initialized = true;

            ScenesChanged?.Invoke(this, EventArgs.Empty);
            ActiveSceneChanged?.Invoke(this, ActiveScene);
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task UpsertAsync(DesktopScene scene, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        await InitializeAsync(cancellationToken);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            // Stage membership changes privately; a failed save must not publish an
            // unsaved world or replace the last successfully stored inventory.
            var pending = new List<DesktopScene>(_scenes);
            var index = pending.FindIndex(s => s.Id.Equals(scene.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) pending[index] = scene; else pending.Add(scene);
            var previousModified = scene.LastModified;
            scene.LastModified = DateTime.UtcNow;
            try { await _repository.SaveAllAsync(pending, cancellationToken); }
            catch
            {
                scene.LastModified = previousModified;
                throw;
            }
            _scenes = pending;
        }
        finally { _writeGate.Release(); }
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var pending = _scenes.Where(s => !s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
            await _repository.SaveAllAsync(pending, cancellationToken);
            _scenes = pending;
            // A failed delete must also retain the active identity and its saved ID.
            if (ActiveScene?.Id.Equals(id, StringComparison.OrdinalIgnoreCase) == true) SetActiveScene(null);
        }
        finally { _writeGate.Release(); }
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Waits for inventory writes already queued and persists the latest active identity.
    /// Unlike background selection saves, failures propagate so shutdown can be cancelled.
    /// Callers should stop accepting new user edits before awaiting this method.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            string? savedId;
            do
            {
                savedId = ActiveScene?.Id;
                await _repository.SaveActiveSceneIdAsync(savedId, cancellationToken);
            }
            while (!string.Equals(savedId, ActiveScene?.Id, StringComparison.Ordinal));
        }
        finally { _writeGate.Release(); }
    }

    public void SetActiveScene(DesktopScene? scene)
    {
        if (ReferenceEquals(ActiveScene, scene)) return;
        ActiveScene = scene;
        _ = PersistActiveSceneAsync(scene);
        ActiveSceneChanged?.Invoke(this, scene);
    }

    private async Task PersistActiveSceneAsync(DesktopScene? scene)
    {
        try { await _repository.SaveActiveSceneIdAsync(scene?.Id); }
        catch { /* active-state persistence must never block world switching */ }
    }
}
