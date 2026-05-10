using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed class SystemIntegrationViewModel : ViewModelBase
{
    private readonly ISystemThemeIntegrator _integrator;

    // ── System info ───────────────────────────────────────────────────────────
    private string _currentAccentHex = "…";
    public string CurrentAccentHex
    {
        get => _currentAccentHex;
        set => SetProperty(ref _currentAccentHex, value);
    }

    private string _windowsBuild = "…";
    public string WindowsBuild
    {
        get => _windowsBuild;
        set => SetProperty(ref _windowsBuild, value);
    }

    private bool _isLightMode;
    public bool IsLightMode
    {
        get => _isLightMode;
        set => SetProperty(ref _isLightMode, value);
    }

    // ── Active theme wallpaper proxy ──────────────────────────────────────────
    public bool ActiveThemeApplyWallpaper
    {
        get => App.ThemeService.ActiveTheme.ApplyToWallpaper;
        set
        {
            App.ThemeService.ActiveTheme.ApplyToWallpaper = value;
            OnPropertyChanged();
        }
    }

    // ── Advanced toggle ───────────────────────────────────────────────────────
    private bool _advancedEnabled;
    public bool AdvancedEnabled
    {
        get => _advancedEnabled;
        set
        {
            if (SetProperty(ref _advancedEnabled, value))
                OnPropertyChanged(nameof(AdvancedEnabledOpacity));
        }
    }

    /// <summary>Fades advanced controls when toggle is off.</summary>
    public double AdvancedEnabledOpacity => _advancedEnabled ? 1.0 : 0.4;

    // ── Status ────────────────────────────────────────────────────────────────
    private string _statusMessage = "Ready.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public SystemIntegrationViewModel(ISystemThemeIntegrator integrator)
    {
        _integrator = integrator;
        App.ThemeService.ThemeChanged += (_, _) =>
            OnPropertyChanged(nameof(ActiveThemeApplyWallpaper));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task RefreshSystemInfoAsync()
    {
        IsBusy = true;
        try
        {
            var info = await _integrator.GetCurrentSystemThemeAsync();
            CurrentAccentHex = info.AccentHex;
            WindowsBuild     = info.WindowsBuild;
            IsLightMode      = info.IsLightMode;
        }
        finally { IsBusy = false; }
    }

    public async Task ApplyAccentColorAsync(string hexColor)
    {
        if (!AdvancedEnabled)
        {
            StatusMessage = "Enable 'Advanced' to apply system-level changes.";
            return;
        }
        IsBusy = true;
        StatusMessage = "Applying accent color…";
        try
        {
            bool ok = await _integrator.ApplyAccentColorAsync(hexColor);
            StatusMessage = ok
                ? "Accent color applied. A sign-out may be required."
                : "Failed to apply accent color — check permissions.";
        }
        finally { IsBusy = false; }
    }

    public async Task ApplyWallpaperAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "No wallpaper path set on the active theme.";
            return;
        }
        IsBusy = true;
        StatusMessage = "Setting wallpaper…";
        try
        {
            bool ok = await _integrator.ApplyWallpaperAsync(path);
            StatusMessage = ok ? "Wallpaper set." : "Failed to set wallpaper.";
        }
        finally { IsBusy = false; }
    }

    public async Task ResetAccentAsync()
    {
        if (!AdvancedEnabled) return;
        IsBusy = true;
        StatusMessage = "Resetting accent to Windows default…";
        try
        {
            bool ok = await _integrator.ResetAccentColorAsync();
            StatusMessage = ok ? "Accent color reset." : "Reset failed.";
        }
        finally { IsBusy = false; }
    }
}
