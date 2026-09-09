using System.Collections.ObjectModel;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed class SkinsViewModel : ViewModelBase, IDisposable
{
    private readonly SkinManagerService _manager;
    private readonly EventHandler _skinsChangedHandler;
    private readonly EventHandler<string> _saveFailedHandler;

    public ObservableCollection<SkinDefinition> Skins { get; } = new();

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public SkinsViewModel(SkinManagerService manager)
    {
        _manager = manager;
        _skinsChangedHandler = (_, _) => RefreshList();
        _manager.SkinsChanged += _skinsChangedHandler;
        _saveFailedHandler = (_, msg) => StatusMessage = msg;
        _manager.SaveFailed += _saveFailedHandler;
        RefreshList();
    }

    public async Task ToggleEnabledAsync(SkinDefinition skin, bool enabled)
    {
        await _manager.SetEnabledAsync(skin, enabled);
        StatusMessage = enabled ? $"\"{skin.Name}\" is now showing on your desktop." : $"\"{skin.Name}\" hidden.";
    }

    public async Task SetOpacityAsync(SkinDefinition skin, double opacity)
    {
        await _manager.SetOpacityAsync(skin, opacity);
    }

    public async Task ToggleClickThroughAsync(SkinDefinition skin, bool enabled)
    {
        await _manager.SetClickThroughAsync(skin, enabled);
        StatusMessage = enabled ? $"\"{skin.Name}\" now lets clicks pass through it." : $"\"{skin.Name}\" catches clicks again.";
    }

    public async Task ToggleLockedAsync(SkinDefinition skin, bool locked)
    {
        await _manager.SetLockedAsync(skin, locked);
        StatusMessage = locked ? $"\"{skin.Name}\" position locked." : $"\"{skin.Name}\" can be dragged again.";
    }

    public async Task ToggleAlwaysOnTopAsync(SkinDefinition skin, bool enabled)
    {
        // Always-on-top is deliberately opt-in. SaveSkinAsync rebuilds the native host so the
        // WinUI presenter receives the new z-order flag, while preserving the persisted X/Y.
        skin.AlwaysOnTop = enabled;
        await _manager.SaveSkinAsync(skin);
        StatusMessage = enabled
            ? $"\"{skin.Name}\" will stay above other windows."
            : $"\"{skin.Name}\" now behaves like a normal desktop widget.";
    }

    public async Task ToggleDesktopLayerAsync(SkinDefinition skin, bool enabled)
    {
        if (!enabled)
        {
            await _manager.SetDesktopLayerAsync(skin, false);
            StatusMessage = $"\"{skin.Name}\" returned to its normal desktop window layer.";
            return;
        }

        StatusMessage = $"Attaching \"{skin.Name}\" behind your desktop icons…";
        bool succeeded = await _manager.SetDesktopLayerAsync(skin, true);
        StatusMessage = succeeded
            ? $"\"{skin.Name}\" is now behind your desktop icons."
            : $"Couldn't attach \"{skin.Name}\" behind the desktop icons on this system — it's staying in the normal desktop window layer.";
    }

    public async Task ResetPositionAsync(SkinDefinition skin)
    {
        await _manager.ResetPositionAsync(skin);
        StatusMessage = $"\"{skin.Name}\" moved back to the top-left corner.";
    }

    private SkinDefinition? _selectedSkin;
    public SkinDefinition? SelectedSkin
    {
        get => _selectedSkin;
        private set => SetProperty(ref _selectedSkin, value);
    }

    public async Task CreateSkinAsync()
    {
        SelectedSkin = await _manager.CreateNewSkinAsync();
        StatusMessage = "New widget created — open the editor to add measures and meters.";
    }

    public async Task DeleteSkinAsync(SkinDefinition skin)
    {
        await _manager.DeleteSkinAsync(skin);
        StatusMessage = $"\"{skin.Name}\" deleted.";
    }

    public void Dispose()
    {
        _manager.SkinsChanged -= _skinsChangedHandler;
        _manager.SaveFailed -= _saveFailedHandler;
    }

    public void ToggleMasterVisibility() => _manager.ToggleAllWidgetsVisibility();

    private void RefreshList()
    {
        var source = _manager.Skins;
        for (int i = 0; i < source.Count; i++)
        {
            if (Skins.Count > i) Skins[i] = source[i];
            else Skins.Add(source[i]);
        }
        while (Skins.Count > source.Count) Skins.RemoveAt(Skins.Count - 1);
    }
}
