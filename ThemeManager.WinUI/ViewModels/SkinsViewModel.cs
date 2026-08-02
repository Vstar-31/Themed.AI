using System.Collections.ObjectModel;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed class SkinsViewModel : ViewModelBase, IDisposable
{
    private readonly SkinManagerService _manager;
    private readonly EventHandler _skinsChangedHandler;

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

        // Store the handler so it can be unsubscribed in Dispose() — same reasoning as
        // ThemesViewModel: an inline lambda here could never be removed, and every SkinsPage
        // navigation would add another permanent listener.
        _skinsChangedHandler = (_, _) => RefreshList();
        _manager.SkinsChanged += _skinsChangedHandler;

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

    /// <summary>Creates a new blank widget and stores it in <see cref="SelectedSkin"/> so the
    /// page can navigate straight to the editor for it.</summary>
    public async Task CreateSkinAsync()
    {
        SelectedSkin = await _manager.CreateNewSkinAsync();
        StatusMessage = "New widget created — open the editor to add measures and meters.";
    }

    public void Dispose()
    {
        _manager.SkinsChanged -= _skinsChangedHandler;
    }

    private void RefreshList()
    {
        Skins.Clear();
        foreach (var s in _manager.Skins)
            Skins.Add(s);
    }
}
