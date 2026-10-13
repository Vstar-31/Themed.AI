using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Personalization;
using ThemeManager.WinUI.Services;
using Microsoft.UI.Xaml.Input;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage : Page
{
    private DesktopScene? _selected;
    private string? _lastWallpaperPath;
    private bool _wallpaperBusy;
    private bool _sceneApplyBusy;
    private bool _studioInitializing;
    private bool _autoApplyAesthetic;
    private bool _worldControlsUpdating;

    // Display row for the WORLDS list. DesktopScene has no notion of "active" — that lives on
    // DesktopSceneService.ActiveScene — so we wrap each scene with a computed IsActive flag at
    // bind time instead of adding UI state to the Core model.
    private sealed class WorldListItem
    {
        public required DesktopScene Scene { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required int WidgetCount { get; init; }
        public required bool IsActive { get; init; }
    }

    private static readonly (string Name, string Description, string Tag)[] Worlds =
    {
        ("Midnight Kyoto", "Rainy neon, quiet motion and a late-night city glow.", "nocturnal"),
        ("Rainy Café", "Warm paper tones and a calm little corner for deep focus.", "cozy"),
        ("Astral Terminal", "A dark systems cockpit with HUD energy and subtle motion.", "hud"),
        ("Neon Afterglow", "Electric color and music-reactive atmosphere for the night.", "neon"),
        ("Arctic Glass", "Cool light, clean glass and almost-silent movement.", "minimal")
    };

    public IReadOnlyList<string> WallpaperStyles { get; } = ThemedWallpaperGenerator.AvailableStyles;

    public IReadOnlyList<string> WallpaperSizes { get; } =
    [
        "1920 × 1080",
        "2560 × 1440",
        "2560 × 1080",
        "3440 × 1440",
        "3840 × 2160",
        "3840 × 1600"
    ];

    public StudioPage()
    {
        InitializeComponent();
        App.SceneService.ScenesChanged += OnScenesChanged;
        App.SceneService.ActiveSceneChanged += OnActiveSceneChanged;
        App.ThemeService.ThemeChanged += ThemeService_ThemeChanged;
    }

    private void ThemeService_ThemeChanged(object? sender, CozyTheme e) =>
        DispatcherQueue.TryEnqueue(UpdateWallpaperStatus);

    private void OnScenesChanged(object? sender, EventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess) DispatcherQueue.TryEnqueue(Refresh); else Refresh();
    }

    // SetActiveScene raises ActiveSceneChanged, not ScenesChanged — without this handler the
    // WORLDS list never re-renders (and never picks up the ACTIVE badge below) when the active
    // world changes from the Desktop World page, a quick-build preset, or Apply to desktop here.
    private void OnActiveSceneChanged(object? sender, DesktopScene? scene)
    {
        if (!DispatcherQueue.HasThreadAccess) DispatcherQueue.TryEnqueue(Refresh); else Refresh();
    }

    private void Refresh()
    {
        var activeId = App.SceneService.ActiveScene?.Id;
        ScenesList.ItemsSource = App.SceneService.Scenes
            .Select(s => new WorldListItem
            {
                Scene = s,
                Name = s.Name,
                Description = s.Description,
                WidgetCount = s.Widgets.Count,
                IsActive = activeId is not null && activeId.Equals(s.Id, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        var scene = _selected is not null
            ? App.SceneService.Scenes.FirstOrDefault(x => x.Id.Equals(_selected.Id, StringComparison.OrdinalIgnoreCase))
            : App.SceneService.ActiveScene ?? App.SceneService.Scenes.FirstOrDefault();
        if (scene is not null)
            Select(scene, false);
    }

    private void Select(DesktopScene scene, bool activate = false)
    {
        _selected = scene;
        if (activate) App.SceneService.SetActiveScene(scene);
        ActiveSceneName.Text = scene.Name;
        WorldStateLabel.Text = App.SceneService.ActiveScene?.Id.Equals(scene.Id, StringComparison.OrdinalIgnoreCase) == true
            ? "ACTIVE WORLD"
            : "SELECTED WORLD";
        var vibe = VibeSnapshotHub.Current;
        var track = string.IsNullOrWhiteSpace(vibe.TrackTitle) ? "No track" : $"{vibe.TrackTitle} · {vibe.Artist}";
        ActiveSceneMeta.Text = $"{scene.Description} · {scene.Widgets.Count} widgets · {scene.Effects.Count} effects · {vibe.Mood} · {track}";
        ApplyStatus.Text = scene.Behavior.ReactToVibeFinder ? "Vibe adaptive · Ready to apply" : "Static world · Ready to apply";
        SceneDetails.Children.Clear();
        AddDetail("Mood", scene.Tags.FirstOrDefault() ?? "aesthetic");
        AddDetail("Widgets", scene.Widgets.Count.ToString());
        AddDetail("Effects", scene.Effects.Count.ToString());
        AddDetail("Adaptation", scene.Behavior.ReactToVibeFinder ? "VibeFinder + media" : "Static");
        AddDetail("Transition", $"{scene.Behavior.TransitionSeconds:0.0}s");
        UpdateWallpaperStatus();
    }

    private void AddDetail(string label, string value)
    {
        SceneDetails.Children.Add(new TextBlock { Text = $"{label}  ·  {value}", FontSize = 13, Opacity = 0.72 });
    }

    private static bool LoadAutoApplyAestheticPreference()
    {
        try
        {
            return Windows.Storage.ApplicationData.Current.LocalSettings.Values.TryGetValue(
                "Studio.AutoApplyAesthetic", out var value) && value is bool enabled && enabled;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveAutoApplyAestheticPreference(bool enabled)
    {
        try
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values["Studio.AutoApplyAesthetic"] = enabled;
        }
        catch
        {
            // Preference failure must never affect Studio navigation or world switching.
        }
    }

    private void SyncStudioState()
    {
        if (_selected is null) return;

        _studioInitializing = true;
        AutoSwitchToggle.IsOn = _selected.Behavior.AutoSwitch;
        AutoApplyAestheticToggle.IsOn = _autoApplyAesthetic;
        _studioInitializing = false;

        ApplyStatus.Text = App.SceneService.ActiveScene?.Id.Equals(_selected.Id, StringComparison.OrdinalIgnoreCase) == true
            ? "This world is active. Browsing is safe; applying is explicit."
            : "Preview only — the desktop will not change until you apply this world.";

        SyncWorldControls();
    }

    private async void AutoApplyAestheticToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_studioInitializing) return;

        _autoApplyAesthetic = AutoApplyAestheticToggle.IsOn;
        SaveAutoApplyAestheticPreference(_autoApplyAesthetic);

        ApplyStatus.Text = _autoApplyAesthetic
            ? "Aesthetic apply is ON — selecting a world applies its theme, wallpaper and layout."
            : "Aesthetic apply is OFF — Studio is preview-only until you press Apply to desktop.";

        if (_autoApplyAesthetic && _selected is not null && !_sceneApplyBusy)
            await ApplySelectedWorldAsync();
    }

    private bool ShouldAutoApplyWorld(DesktopScene scene)
        => scene.Behavior.ApplyAestheticOnSelection ?? _autoApplyAesthetic;

    private void SyncWorldControls()
    {
        if (_selected is null) return;

        _worldControlsUpdating = true;
        try
        {
            SelectedWorldAestheticToggle.IsOn = _selected.Behavior.ApplyAestheticOnSelection ?? _autoApplyAesthetic;

            WorldVibePrimaryToggle.IsOn = IsWorldWidgetVisible(_selected, "VibeFinder Primary");
            WorldVibeMinimalToggle.IsOn = IsWorldWidgetVisible(_selected, "VibeFinder Minimal");
            WorldVibePlaylistToggle.IsOn = IsWorldWidgetVisible(_selected, "VibeFinder Playlist");
        }
        finally
        {
            _worldControlsUpdating = false;
        }
    }

    private bool IsWorldWidgetVisible(DesktopScene scene, string widgetName)
    {
        var skin = App.SkinManager?.Skins.FirstOrDefault(s =>
            s.Name.Equals(widgetName, StringComparison.OrdinalIgnoreCase));
        if (skin is null) return false;

        var placement = scene.Widgets.FirstOrDefault(p =>
            p.WidgetId.Equals(skin.Id, StringComparison.OrdinalIgnoreCase));

        return placement?.Visible == true;
    }

    private static void EnsureWorldWidgetPlacement(DesktopScene scene, SkinDefinition skin)
    {
        if (scene.Widgets.Any(p => p.WidgetId.Equals(skin.Id, StringComparison.OrdinalIgnoreCase)))
            return;

        scene.Widgets.Add(new SceneWidgetPlacement
        {
            WidgetId = skin.Id,
            X = skin.X,
            Y = skin.Y,
            Scale = 1.0,
            Rotation = 0,
            Opacity = Math.Clamp(skin.Opacity, 0, 1),
            ZIndex = scene.Widgets.Count == 0 ? 0 : scene.Widgets.Max(p => p.ZIndex) + 1,
            Visible = false,
            Monitor = "Primary",
            Definition = skin.Clone()
        });
    }

    private async void SelectedWorldAestheticToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_worldControlsUpdating || _selected is null) return;

        _selected.Behavior.ApplyAestheticOnSelection = SelectedWorldAestheticToggle.IsOn;
        await App.SceneService.UpsertAsync(_selected);

        ApplyStatus.Text = SelectedWorldAestheticToggle.IsOn
            ? $"{_selected.Name}: automatic aesthetic apply enabled."
            : $"{_selected.Name}: automatic aesthetic apply disabled.";

        if (SelectedWorldAestheticToggle.IsOn && !_sceneApplyBusy)
            await ApplySelectedWorldAsync();
    }

    private async void WorldVibeWidgetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_worldControlsUpdating || _selected is null) return;
        if (sender is not ToggleSwitch toggle || toggle.Tag is not string widgetName) return;

        var skin = App.SkinManager?.Skins.FirstOrDefault(s =>
            s.Name.Equals(widgetName, StringComparison.OrdinalIgnoreCase));
        if (skin is null) return;

        try
        {
            toggle.IsEnabled = false;
            EnsureWorldWidgetPlacement(_selected, skin);

            var placement = _selected.Widgets.First(p =>
                p.WidgetId.Equals(skin.Id, StringComparison.OrdinalIgnoreCase));
            placement.Visible = toggle.IsOn;
            placement.Opacity = Math.Clamp(placement.Opacity, 0, 1);
            placement.Definition ??= skin.Clone(skin.Id);

            await App.SceneService.UpsertAsync(_selected);

            var sceneService = App.SceneService;
            var skinManager = App.SkinManager;
            if (sceneService.ActiveScene?.Id.Equals(_selected.Id, StringComparison.OrdinalIgnoreCase) == true &&
                skinManager is not null)
                await skinManager.SetEnabledAsync(skin, toggle.IsOn);

            ApplyStatus.Text = toggle.IsOn
                ? $"{widgetName} enabled for {_selected.Name}."
                : $"{widgetName} disabled for {_selected.Name}.";
        }
        catch (Exception ex)
        {
            ApplyStatus.Text = $"Could not change {widgetName}: {ex.Message}";
            SyncWorldControls();
        }
        finally
        {
            toggle.IsEnabled = true;
        }
    }

    private async Task SaveCurrentSceneLayoutAsync(DesktopScene scene)
    {
        if (App.SkinManager is null) return;

        var byId = App.SkinManager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var placement in scene.Widgets)
        {
            if (!byId.TryGetValue(placement.WidgetId, out var skin)) continue;

            if (placement.X != skin.X ||
                placement.Y != skin.Y ||
                Math.Abs(placement.Opacity - skin.Opacity) > 0.001 ||
                placement.Visible != skin.Enabled)
                changed = true;

            placement.X = skin.X;
            placement.Y = skin.Y;
            placement.Opacity = Math.Clamp(skin.Opacity, 0, 1);
            placement.Visible = skin.Enabled;
            placement.Definition = skin.Clone(skin.Id);
        }

        if (changed)
            await App.SceneService.UpsertAsync(scene);
    }

    private void UpdateWallpaperStatus()
    {
        if (_selected is null) return;
        var wallpaper = _selected.WallpaperPath;
        if (!string.IsNullOrWhiteSpace(wallpaper) && File.Exists(wallpaper))
            WallpaperStatusText.Text = $"Bound to this world ✓  {Path.GetFileName(wallpaper)}";
        else
            WallpaperStatusText.Text = $"No wallpaper bound. Active theme: {App.ThemeService.ActiveTheme.Name} · {App.ThemeService.ActiveTheme.AccentPrimary} · generation is fully local/free.";
    }

    private async Task SeedStarterWorldsAsync()
    {
        if (App.SceneService.Scenes.Count > 0) return;

        var widgets = CaptureCurrentWidgetLayout();
        for (var i = 0; i < Worlds.Length; i++)
        {
            var pick = Worlds[i];
            var scene = new DesktopScene
            {
                Name = pick.Name,
                Description = pick.Description,
                ThemeId = App.ThemeService.ActiveTheme.Id,
                Tags = new List<string> { pick.Tag, "starter", "vibe" },
                // CaptureCurrentWidgetLayout already includes a deep widget-definition snapshot.
                // Clone the placement so every starter world owns its own independent composition
                // rather than sharing nested definitions between worlds.
                Widgets = widgets.Select(ClonePlacement).ToList(),
                Effects = new List<SceneEffect>
                {
                    new() { Type = pick.Tag == "minimal" ? "Glass" : "Glow", Intensity = pick.Tag is "neon" or "hud" ? 0.8 : 0.35 },
                    new() { Type = "AmbientMotion", Intensity = 0.25, Speed = 0.65 }
                },
                Behavior = new SceneBehavior { ReactToVibeFinder = true, ReactToMedia = true, TransitionSeconds = 0.9 }
            };
            await App.SceneService.UpsertAsync(scene);
        }
    }

    private async void GenerateWorld_Click(object sender, RoutedEventArgs e)
        => await GeneratePromptWorldAsync();

    private async void WorldPromptBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        await GeneratePromptWorldAsync();
    }

    private void WorldPromptChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string prompt } && !string.IsNullOrWhiteSpace(prompt))
        {
            WorldPromptBox.Text = prompt;
            WorldPromptBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            WorldPromptBox.SelectionStart = WorldPromptBox.Text.Length;
        }
    }

    private async Task GeneratePromptWorldAsync()
    {
        var prompt = WorldPromptBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ApplyStatus.Text = "Describe the desktop you want first — the prompt drives its palette, wallpaper and widget mix.";
            WorldPromptBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            return;
        }

        await CreateWorldAsync(true, prompt);
    }

    private async Task CreateWorldAsync(bool generateWallpaper, string prompt)
    {
        prompt = prompt.Trim();
        var lower = prompt.ToLowerInvariant();
        var tag = InferWorldTag(lower);
        var minimal = ContainsAny(lower, "minimal", "minimalist", "only", "just", "clean", "focus");
        var dark = ContainsAny(lower, "dark", "midnight", "night", "noir", "neon", "cyberpunk", "space");
        var light = ContainsAny(lower, "light", "bright", "airy", "daylight", "pastel", "white");
        var wantsMotion = ContainsAny(lower, "animated", "motion", "energetic", "dynamic", "alive");
        var wantsMusic = ContainsAny(lower, "music", "playlist", "player", "song", "spotify", "vibe", "listen", "audio");

        var context = new GenerationContext
        {
            Prompt = prompt,
            Constraints = new GenerationConstraints
            {
                MustBeDarkTheme = dark && !light,
                MustBeLightTheme = light && !dark,
                Minimalist = minimal,
                MaxWidgets = minimal ? 3 : 5
            }
        };

        ApplyStatus.Text = "Designing a palette and composing your desktop world…";
        try
        {
            var candidate = await Task.Run(() => App.Personalization.GenerateBestTheme(context));
            var generatedTheme = candidate.Theme;
            generatedTheme.Description = $"Palette generated from: {prompt}";
            await App.ThemeService.SaveThemeAsync(generatedTheme);

            var scene = new DesktopScene
            {
                Name = BuildWorldName(generatedTheme.Name),
                Description = prompt,
                ThemeId = generatedTheme.Id,
                Tags = new List<string> { tag, "generated", "prompt-generated" },
                Widgets = CaptureWidgetLayoutForPrompt(prompt, minimal ? 3 : 5),
                Effects = new List<SceneEffect>
                {
                    new()
                    {
                        Type = tag == "minimal" ? "Glass" : tag is "neon" or "hud" ? "Glow" : "AmbientMotion",
                        Intensity = wantsMotion ? 0.8 : minimal ? 0.12 : tag is "neon" or "hud" ? 0.58 : 0.28,
                        Speed = wantsMotion ? 0.9 : 0.4
                    },
                    new()
                    {
                        Type = "AmbientMotion",
                        Intensity = wantsMotion ? 0.55 : minimal ? 0.08 : 0.22,
                        Speed = wantsMotion ? 0.85 : 0.35
                    }
                },
                Behavior = new SceneBehavior
                {
                    ReactToVibeFinder = wantsMusic,
                    ReactToMedia = true,
                    ReactToWeather = ContainsAny(lower, "weather", "rain", "storm", "forecast"),
                    MotionIntensity = wantsMotion ? 0.8 : minimal ? 0.1 : 0.3,
                    TransitionSeconds = ContainsAny(lower, "snappy", "instant", "fast") ? 0.35 : 0.9
                }
            };

            await App.SceneService.UpsertAsync(scene);
            Select(scene);

            if (generateWallpaper)
            {
                WallpaperStyleCombo.SelectedItem = WallpaperStyleForTag(tag);
                WallpaperNameBox.Text = scene.Name;
                WallpaperStatusText.Text = $"Generating wallpaper from the new “{generatedTheme.Name}” palette…";
                await GenerateWallpaperAsync(false);
            }

            ApplyStatus.Text = $"Created “{scene.Name}” from your prompt. Review its widgets and press Apply to desktop when ready.";
        }
        catch (Exception ex)
        {
            ApplyStatus.Text = $"World generation failed: {ex.Message}";
            App.LoggerFactory.CreateLogger<StudioPage>().LogError(ex, "Prompt-to-world generation failed for {Prompt}", prompt);
        }
    }

    private static string InferWorldTag(string prompt)
    {
        if (ContainsAny(prompt, "neon", "cyberpunk", "synthwave", "tokyo", "magenta", "vaporwave")) return "neon";
        if (ContainsAny(prompt, "hud", "terminal", "sci-fi", "futuristic", "cyber", "spaceship", "dashboard")) return "hud";
        if (ContainsAny(prompt, "cozy", "cafe", "café", "warm", "autumn", "library", "reading", "linen")) return "cozy";
        if (ContainsAny(prompt, "arctic", "ice", "snow", "glacier", "minimal", "minimalist", "zen", "focus", "clean")) return "minimal";
        return ContainsAny(prompt, "rain", "night", "midnight", "city", "storm", "space", "noir", "moody") ? "nocturnal" : "cozy";
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static string BuildWorldName(string? themeName)
    {
        var clean = string.IsNullOrWhiteSpace(themeName) ? "New Desktop" : themeName.Trim();
        clean = clean.EndsWith("World", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean} World";
        return clean.Length <= 48 ? clean : clean[..48].TrimEnd();
    }

    private List<SceneWidgetPlacement> CaptureWidgetLayoutForPrompt(string prompt, int maxWidgets)
    {
        var available = App.SkinManager?.Skins ?? new List<SkinDefinition>();
        var lower = prompt.ToLowerInvariant();
        var chosen = new List<SkinDefinition>();
        bool explicitWidgetIntent = ContainsAny(lower, "clock", "time", "date", "cpu", "processor", "ram", "memory", "disk", "storage", "system stats", "performance", "network", "wifi", "bandwidth", "uptime", "music", "playlist", "player", "song", "spotify", "vibe", "widget");
        bool wantsNoWidgets = ContainsAny(lower, "no widgets", "without widgets", "wallpaper only", "background only");

        void AddFirst(params string[] nameHints)
        {
            var match = available.FirstOrDefault(s => nameHints.Any(h =>
                s.Name.Contains(h, StringComparison.OrdinalIgnoreCase)));
            if (match is not null && chosen.All(s => !s.Id.Equals(match.Id, StringComparison.OrdinalIgnoreCase)))
                chosen.Add(match);
        }

        if (wantsNoWidgets) return new List<SceneWidgetPlacement>();

        if (ContainsAny(lower, "clock", "time", "date")) AddFirst("clock");
        if (ContainsAny(lower, "cpu", "processor", "ram", "memory", "disk", "storage", "system stats", "performance")) AddFirst("system monitor", "system");
        if (ContainsAny(lower, "network", "wifi", "bandwidth", "internet")) AddFirst("network");
        if (ContainsAny(lower, "uptime")) AddFirst("uptime");
        if (ContainsAny(lower, "music", "playlist", "player", "song", "spotify", "vibe", "listen", "audio"))
        {
            if (ContainsAny(lower, "playlist", "full player", "music dashboard")) AddFirst("vibefinder playlist");
            else if (ContainsAny(lower, "minimal", "compact", "pill")) AddFirst("vibefinder minimal");
            else AddFirst("vibefinder primary");
        }

        if (chosen.Count == 0 && !explicitWidgetIntent)
            chosen = available.Where(s => s.Enabled).Take(maxWidgets).ToList();
        if (chosen.Count == 0 && !wantsNoWidgets)
            chosen = available.Where(s => s.Enabled).Take(maxWidgets).ToList();

        chosen = chosen.Take(maxWidgets).ToList();
        if (chosen.Count == 0) return new List<SceneWidgetPlacement>();

        return chosen.Select((skin, index) => new SceneWidgetPlacement
        {
            WidgetId = skin.Id,
            X = skin.X,
            Y = skin.Y,
            Scale = 1.0,
            Rotation = 0,
            Opacity = Math.Clamp(skin.Opacity, 0, 1),
            ZIndex = index,
            Visible = true,
            Monitor = "Primary",
            Definition = skin.Clone(skin.Id)
        }).ToList();
    }

    private static string WallpaperStyleForTag(string tag) => tag switch
    {
        "nocturnal" => "Orbital Night",
        "cozy" => "Cozy Aurora",
        "hud" => "Glass Mesh",
        "neon" => "Neon Glow",
        "minimal" => "Zen Minimal",
        _ => "Soft Gradient"
    };

    private List<SceneWidgetPlacement> CaptureCurrentWidgetLayout() =>
        App.SkinManager?.Skins
            .Where(w => w.Enabled)
            .Select((w, index) => new SceneWidgetPlacement
            {
                WidgetId = w.Id,
                X = w.X,
                Y = w.Y,
                Scale = 1.0,
                Rotation = 0,
                Opacity = Math.Clamp(w.Opacity, 0, 1),
                ZIndex = index,
                Visible = w.Enabled,
                Monitor = "Primary",
                Definition = w.Clone()
            })
            .ToList() ?? new List<SceneWidgetPlacement>();

    private static readonly string[] SurprisePrompts =
    [
        "A rainy cyberpunk night with neon magenta and cyan, a music player and system stats",
        "A cozy autumn reading nook with warm paper colors, clock and uptime",
        "A bright minimal focus desk with lots of whitespace and only a clock",
        "An arctic glass workstation with icy blue accents, system monitor and network stats",
        "A dark orbital terminal with a subtle HUD, uptime and a compact music pill"
    ];

    private async void SurpriseMe_Click(object sender, RoutedEventArgs e)
    {
        var prompt = SurprisePrompts[Random.Shared.Next(SurprisePrompts.Length)];
        WorldPromptBox.Text = prompt;
        await CreateWorldAsync(true, prompt);
        await ApplySelectedWorldAsync();
    }

    private async void ApplyScene_Click(object sender, RoutedEventArgs e) => await ApplySelectedWorldAsync();

    private async void ScenesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScenesList.SelectedItem is not WorldListItem item) return;

        // Save the outgoing world's current positions before optionally moving into the selected world.
        var outgoing = App.SceneService.ActiveScene;
        if (outgoing is not null && !outgoing.Id.Equals(item.Scene.Id, StringComparison.OrdinalIgnoreCase))
            await SaveCurrentSceneLayoutAsync(outgoing);

        Select(item.Scene, false);

        UpdateAutoSwitchUi();
        UpdateSetActiveButtonState();

        var selected = _selected;
        if (selected is not null && ShouldAutoApplyWorld(selected) && !_sceneApplyBusy)
            await ApplySelectedWorldAsync();
        else
            SyncStudioState();
    }

    private async void SaveCurrentLayout_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ApplyStatus.Text = "Select a world first.";
            return;
        }

        _selected.Widgets = CaptureCurrentWidgetLayout();
        await App.SceneService.UpsertAsync(_selected);
        Select(_selected, false);
        ApplyStatus.Text = "Current desktop layout saved to this world ✓";
    }

    private async void DuplicateWorld_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var copy = new DesktopScene
        {
            Name = _selected.Name + " Copy",
            Description = _selected.Description,
            Author = _selected.Author,
            Tags = _selected.Tags.ToList(),
            ThemeId = _selected.ThemeId,
            WallpaperPath = _selected.WallpaperPath,
            WallpaperOpacity = _selected.WallpaperOpacity,
            WallpaperFit = _selected.WallpaperFit,
            Widgets = _selected.Widgets.Select(ClonePlacement).ToList(),
            Effects = _selected.Effects.Select(CloneEffect).ToList(),
            Behavior = CloneBehavior(_selected.Behavior)
        };

        await App.SceneService.UpsertAsync(copy);
        Select(copy);
        ApplyStatus.Text = $"Created {copy.Name} ✓";
    }

    private async void DeleteWorld_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete this world?",
            Content = $"\"{_selected.Name}\" will be removed permanently. Your widget definitions are not deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var deletedName = _selected.Name;
        var deletedId = _selected.Id;
        await App.SceneService.DeleteAsync(deletedId);
        _selected = App.SceneService.ActiveScene ?? App.SceneService.Scenes.FirstOrDefault();
        if (_selected is not null) Select(_selected, false);
        ApplyStatus.Text = $"Deleted {deletedName}.";
    }

    private static SceneWidgetPlacement ClonePlacement(SceneWidgetPlacement source) => new()
    {
        WidgetId = source.WidgetId,
        X = source.X,
        Y = source.Y,
        Scale = source.Scale,
        Rotation = source.Rotation,
        Opacity = source.Opacity,
        ZIndex = source.ZIndex,
        Visible = source.Visible,
        Monitor = source.Monitor,
        Definition = source.Definition?.Clone()
    };

    private static SceneEffect CloneEffect(SceneEffect source) => new()
    {
        Type = source.Type,
        Intensity = source.Intensity,
        Speed = source.Speed,
        Enabled = source.Enabled,
        Parameters = source.Parameters.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
    };

    private static SceneBehavior CloneBehavior(SceneBehavior source) => new()
    {
        FollowWindowsTheme = source.FollowWindowsTheme,
        ReactToMedia = source.ReactToMedia,
        ReactToVibeFinder = source.ReactToVibeFinder,
        ReactToWeather = source.ReactToWeather,
        AutoSwitch = source.AutoSwitch,
        ApplyAestheticOnSelection = source.ApplyAestheticOnSelection,
        TransitionSeconds = source.TransitionSeconds,
        MotionIntensity = source.MotionIntensity,
        AudioSensitivity = source.AudioSensitivity
    };

    private async Task GenerateWallpaperAsync(bool applyAfterGeneration)
    {
        if (_selected is null)
        {
            WallpaperStatusText.Text = "Select a world first.";
            return;
        }

        if (_wallpaperBusy) return;
        _wallpaperBusy = true;

        try
        {
            var style = WallpaperStyleCombo.SelectedItem as string ?? WallpaperStyles[0];
            var (width, height) = ParseSize(WallpaperSizeCombo.SelectedItem as string);
            var name = string.IsNullOrWhiteSpace(WallpaperNameBox.Text)
                ? _selected.Name
                : WallpaperNameBox.Text.Trim();
            var worldTheme = App.ThemeService.Themes.FirstOrDefault(t =>
                t.Id.Equals(_selected.ThemeId, StringComparison.OrdinalIgnoreCase))
                ?? App.ThemeService.ActiveTheme;

            WallpaperStatusText.Text = $"Generating {style} wallpaper from {worldTheme.Name}…";
            var path = await ThemedWallpaperGenerator.GenerateAsync(
                worldTheme,
                style,
                width,
                height,
                name);

            _lastWallpaperPath = path;
            _selected.WallpaperPath = path;
            _selected.WallpaperFit = "Fill";
            _selected.WallpaperOpacity = 1.0;
            await App.SceneService.UpsertAsync(_selected);

            if (applyAfterGeneration)
            {
                WallpaperStatusText.Text = "Generated ✓ · applying to Windows…";
                var applied = await App.SystemIntegrator.ApplyWallpaperAsync(path);
                WallpaperStatusText.Text = applied
                    ? $"Applied ✓  {Path.GetFileName(path)} · bound to {_selected.Name}"
                    : $"Generated ✓ but Windows did not accept the wallpaper change. Saved at {path}";
            }
            else
            {
                WallpaperStatusText.Text = $"Generated ✓  {Path.GetFileName(path)} · bound to {_selected.Name}";
            }
        }
        catch (OperationCanceledException)
        {
            WallpaperStatusText.Text = "Wallpaper generation cancelled.";
        }
        catch (Exception ex)
        {
            WallpaperStatusText.Text = $"Wallpaper generation failed: {ex.Message}";
        }
        finally
        {
            _wallpaperBusy = false;
        }
    }

    private async void GenerateWallpaper_Click(object sender, RoutedEventArgs e) =>
        await GenerateWallpaperAsync(false);

    private async void GenerateAndApplyWallpaper_Click(object sender, RoutedEventArgs e) =>
        await GenerateWallpaperAsync(true);

    private async void ApplyLastWallpaper_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastWallpaperPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            path = _selected?.WallpaperPath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            WallpaperStatusText.Text = "No generated wallpaper is available for this world yet.";
            return;
        }

        try
        {
            var applied = await App.SystemIntegrator.ApplyWallpaperAsync(path);
            WallpaperStatusText.Text = applied
                ? $"Applied ✓  {Path.GetFileName(path)}"
                : "Windows rejected the wallpaper change.";
        }
        catch (Exception ex)
        {
            WallpaperStatusText.Text = $"Could not apply wallpaper: {ex.Message}";
        }
    }

    private static (int Width, int Height) ParseSize(string? value)
    {
        var numbers = value?
            .Replace("×", "x", StringComparison.Ordinal)
            .Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (numbers is { Length: 2 } &&
            int.TryParse(numbers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) &&
            int.TryParse(numbers[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return (width, height);
        }

        return (1920, 1080);
    }

    private async Task ApplySelectedWorldAsync()
    {
        if (_selected is null || _sceneApplyBusy) return;
        _sceneApplyBusy = true;

        try
        {
            ApplyStatus.Text = $"Applying {_selected.Name}…";

            var previous = App.SceneService.ActiveScene;
            if (previous is not null && !previous.Id.Equals(_selected.Id, StringComparison.OrdinalIgnoreCase))
                await SaveCurrentSceneLayoutAsync(previous);

            var themes = await App.ThemeRepository.LoadAllAsync();
            var targetTheme = themes.FirstOrDefault(t => t.Id.Equals(_selected.ThemeId, StringComparison.OrdinalIgnoreCase));

            if (targetTheme is not null)
            {
                App.ThemeService.SetActiveTheme(targetTheme);
                await App.SystemIntegrator.ApplyAccentColorAsync(CozyTheme.NormalizeHex(targetTheme.AccentPrimary));
            }

            if (!string.IsNullOrWhiteSpace(_selected.WallpaperPath) && File.Exists(_selected.WallpaperPath))
            {
                var applied = await App.SystemIntegrator.ApplyWallpaperAsync(_selected.WallpaperPath);
                WallpaperStatusText.Text = applied
                    ? $"Applied ✓  {Path.GetFileName(_selected.WallpaperPath)} · bound to {_selected.Name}"
                    : $"World wallpaper saved, but Windows rejected the change: {Path.GetFileName(_selected.WallpaperPath)}";
            }
            else if (targetTheme is not null && targetTheme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(targetTheme.WallpaperPath) && File.Exists(targetTheme.WallpaperPath))
            {
                var applied = await App.SystemIntegrator.ApplyWallpaperAsync(targetTheme.WallpaperPath);
                WallpaperStatusText.Text = applied
                    ? $"Applied theme wallpaper ✓  {Path.GetFileName(targetTheme.WallpaperPath)}"
                    : "Theme wallpaper was not accepted by Windows.";
            }

            if (App.SkinManager is not null)
            {
                var result = await App.SkinManager.ApplySceneAsync(_selected.Widgets);

                // Backfill snapshots for older worlds whose widget ids still resolve locally. This
                // turns a successful legacy apply into a self-contained world without changing its
                // saved placement geometry.
                var currentWidgets = App.SkinManager.Skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
                var snapshotsChanged = false;
                foreach (var placement in _selected.Widgets)
                {
                    if (placement.Definition is null &&
                        currentWidgets.TryGetValue(placement.WidgetId, out var resolvedWidget))
                    {
                        placement.Definition = resolvedWidget.Clone();
                        snapshotsChanged = true;
                    }
                }

                if (snapshotsChanged)
                    await App.SceneService.UpsertAsync(_selected);

                ApplyStatus.Text = result.Missing > 0
                    ? $"Applied {result.Applied}/{_selected.Widgets.Count} widgets · {result.Missing} unavailable"
                    : $"Applied {result.Applied} widgets to desktop ✓";
            }
            else
            {
                ApplyStatus.Text = "Applied world theme and wallpaper ✓";
            }

            App.SceneService.SetActiveScene(_selected);
            ActiveSceneMeta.Text = $"Applied · {_selected.Widgets.Count} saved widget placements · VibeFinder adaptation {(_selected.Behavior.ReactToVibeFinder ? "on" : "off")}";
        }
        catch (Exception ex)
        {
            ApplyStatus.Text = $"World apply failed: {ex.Message}";
        }
        finally
        {
            _sceneApplyBusy = false;
        }
    }
}
