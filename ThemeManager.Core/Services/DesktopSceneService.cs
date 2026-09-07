using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

public sealed class DesktopSceneService
{
    private readonly DesktopSceneRepository _repository;
    private List<DesktopScene> _scenes = new();

    public IReadOnlyList<DesktopScene> Scenes => _scenes;
    public DesktopScene? ActiveScene { get; private set; }
    public event EventHandler<DesktopScene?>? ActiveSceneChanged;
    public event EventHandler? ScenesChanged;

    public DesktopSceneService(DesktopSceneRepository repository) => _repository = repository;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _scenes = await _repository.LoadAllAsync(cancellationToken);
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpsertAsync(DesktopScene scene, CancellationToken cancellationToken = default)
    {
        scene.LastModified = DateTime.UtcNow;
        var index = _scenes.FindIndex(s => s.Id.Equals(scene.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _scenes[index] = scene; else _scenes.Add(scene);
        await _repository.SaveAllAsync(_scenes, cancellationToken);
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _scenes.RemoveAll(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (ActiveScene?.Id.Equals(id, StringComparison.OrdinalIgnoreCase) == true) SetActiveScene(null);
        await _repository.SaveAllAsync(_scenes, cancellationToken);
        ScenesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActiveScene(DesktopScene? scene)
    {
        ActiveScene = scene;
        ActiveSceneChanged?.Invoke(this, scene);
    }
}
