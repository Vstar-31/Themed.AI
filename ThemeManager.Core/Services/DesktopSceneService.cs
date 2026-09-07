using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

public sealed class DesktopSceneService
{
    private readonly DesktopSceneRepository _repository;
    private List<DesktopScene> _scenes = new();
    private bool _initialized;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public IReadOnlyList<DesktopScene> Scenes => _scenes;
    public DesktopScene? ActiveScene { get; private set; }
    public event EventHandler<DesktopScene?>? ActiveSceneChanged;
    public event EventHandler? ScenesChanged;

    public DesktopSceneService(DesktopSceneRepository repository) => _repository = repository;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        _scenes = await _repository.LoadAllAsync(cancellationToken);
        _initialized = true;
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpsertAsync(DesktopScene scene, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        await InitializeAsync(cancellationToken);
        scene.LastModified = DateTime.UtcNow;
        var index = _scenes.FindIndex(s => s.Id.Equals(scene.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _scenes[index] = scene; else _scenes.Add(scene);
        await PersistAsync(cancellationToken);
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        _scenes.RemoveAll(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (ActiveScene?.Id.Equals(id, StringComparison.OrdinalIgnoreCase) == true) SetActiveScene(null);
        await PersistAsync(cancellationToken);
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActiveScene(DesktopScene? scene)
    {
        if (ReferenceEquals(ActiveScene, scene)) return;
        ActiveScene = scene;
        ActiveSceneChanged?.Invoke(this, scene);
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try { await _repository.SaveAllAsync(_scenes, cancellationToken); }
        finally { _writeGate.Release(); }
    }
}
