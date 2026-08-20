# Themed.AI 🍵

**A lightweight, modular, minimalistic Windows 11 theme manager**  
Built with WinUI 3 · Windows App SDK · .NET 8 · MVVM

---

## Design Philosophy

Themed.AI is intentionally minimal:

- **Cozy aesthetic** — warm neutrals, generous whitespace, and iOS-style rounded corners inspired by macOS System Settings.
- **Lightweight** — fast cold start, low idle memory, no heavy DI frameworks.
- **Modular** — adding a new theme action means adding a ViewModel method and a XAML toggle. Nothing else touches.
- **100 % free toolchain** — Visual Studio Community + Windows App SDK + .NET 8. No paid licenses required.

---

## Project Structure

```
ThemeManager.sln
│
├── ThemeManager.Core/                  # Platform-agnostic; no WinUI dependency
│   ├── Models/
│   │   └── CozyTheme.cs               # Theme data model + CozyDefaults factory
│   └── Services/
│       ├── ThemeRepository.cs         # JSON load/save (atomic write, auto-seed)
│       ├── ThemeService.cs            # Active theme state + change notifications
│       └── ISystemThemeIntegrator.cs  # OS-integration contract
│
├── ThemeManager.Integration/          # Windows-specific OS calls
│   └── SystemThemeIntegrator.cs      # DWM registry writes, wallpaper API, accent read
│
└── ThemeManager.WinUI/                # WinUI 3 front-end
    ├── Resources/
    │   └── ThemeResources.xaml        # Full design-system token dictionary
    ├── ViewModels/
    │   ├── ViewModelBase.cs           # INotifyPropertyChanged + dispatcher helper
    │   ├── ThemesViewModel.cs         # Theme list CRUD
    │   ├── ThemeEditorViewModel.cs    # Live palette editing
    │   └── SystemIntegrationViewModel.cs
    ├── Views/
    │   ├── ColorTokenRow.xaml/.cs     # Reusable color-picker row control
    │   ├── ThemesPage.xaml/.cs        # Theme grid with palette strip cards
    │   ├── ThemeEditorPage.xaml/.cs   # Split editor + live desktop preview
    │   ├── SystemIntegrationPage.xaml/.cs
    │   └── SettingsPage.xaml/.cs
    ├── Converters/Converters.cs       # HexToBrush, BoolToVisibility, InverseBool
    ├── App.xaml/.cs                   # Bootstrap, live ApplyThemeToResources()
    └── MainWindow.xaml/.cs            # Sidebar shell + Frame navigation
```

---

## Palette — Cozy Café (Built-in Default)

| Token              | Name      | Hex       | Role                              |
|--------------------|-----------|-----------|-----------------------------------|
| `BackgroundBase`   | Linen     | `#F5F1EA` | App window background             |
| `BackgroundAlt`    | Khaki     | `#D7C9B8` | Sidebar, secondary cards          |
| `Surface`          | Camel     | `#B2967D` | Filled buttons, sliders, controls |
| `AccentPrimary`    | Cocoa     | `#7D5A44` | Interactive accent, links         |
| `AccentStrong`     | Espresso  | `#4A342A` | Headers, strong emphasis          |
| `TextPrimary`      | —         | `#3B2A20` | Body text                         |
| `TextMuted`        | —         | `#7F7065` | Captions, placeholders            |
| `BorderSubtle`     | —         | `#E0D5C7` | Card borders, dividers            |

---

## How Live Theming Works

```
User picks a color
        │
        ▼
ColorTokenRow.PushChange(hex)
        │   (sets HexValue DependencyProperty)
        │
        ▼
ThemeEditorViewModel property setter
        │   (updates working CozyTheme + calls)
        │
        ▼
ThemeService.NotifyThemeTokenChanged()
        │   (fires ThemeChanged event)
        │
        ▼
App.ApplyThemeToResources(theme)
        │   (overwrites ResourceDictionary entries)
        │
        ▼
All SolidColorBrush references across the visual tree update instantly
(no restart, no navigation, no re-render trigger needed)
```

---

## Prerequisites

| Tool                         | Minimum version | Free?  |
|------------------------------|-----------------|--------|
| Visual Studio Community 2022 | 17.9+           | ✅ Yes |
| Windows App SDK workload     | 1.5+            | ✅ Yes |
| .NET 8 SDK                   | 8.0             | ✅ Yes |
| Windows 11 (or 10 19041+)    | —               | —      |

### Install the workload

In the VS Installer, select:
> **Windows application development** → ✅ Windows App SDK C# Templates

Or via CLI:
```powershell
winget install Microsoft.WindowsAppRuntimeInstaller
```

---

## Build & Run

```powershell
# 1. Clone
git clone https://github.com/your-org/ThemedAI.git
cd ThemedAI

# 2. Restore
dotnet restore ThemeManager.sln

# 3. Build (x64 only — WinUI 3 requires a specific RID)
dotnet build ThemeManager.sln -c Debug -r win-x64

# 4. Run (packaged MSIX requires VS F5 or Deploy first)
#    Open ThemeManager.sln in Visual Studio → Set ThemeManager.WinUI as startup → F5
```

> **Why Visual Studio for the first run?**  
> MSIX packaged apps need the Windows App SDK deployment framework registered.  
> VS does this automatically on F5. After that, you can use `dotnet run` for  
> non-packaged iterations.

---

## Theme JSON Schema

Themes are stored at:
```
%LOCALAPPDATA%\ThemedAI\themes.json
```

Each theme follows this schema (extend `customTokens` freely):

```json
{
  "id":               "cozy-default",
  "name":             "Cozy Café",
  "description":      "Warm linen and espresso tones.",
  "lastModified":     "2024-01-01T00:00:00Z",
  "isBuiltIn":        true,
  "backgroundBase":   "#F5F1EA",
  "backgroundAlt":    "#D7C9B8",
  "surface":          "#B2967D",
  "accentPrimary":    "#7D5A44",
  "accentStrong":     "#4A342A",
  "textPrimary":      "#3B2A20",
  "textMuted":        "#7F7065",
  "borderSubtle":     "#E0D5C7",
  "cornerRadiusScale": 1.0,
  "densityScale":      1.0,
  "applyToSystemAccent": false,
  "applyToWallpaper":    false,
  "wallpaperPath":    null,
  "customTokens":     {}
}
```

---

## Adding a New Theme Token (Extension Guide)

1. **Model** — add a `public string MyNewToken { get; set; }` property to `CozyTheme.cs`.
2. **Default** — set its default value in `CozyDefaults.CreateDefault()`.
3. **Persistence** — `System.Text.Json` picks it up automatically (no migration needed for new fields; old files just get the default).
4. **Apply** — add `resources["MyNewBrush"] = Brush(theme.MyNewToken);` in `App.ApplyThemeToResources()`.
5. **Resource** — declare `<SolidColorBrush x:Key="MyNewBrush" ... />` in `ThemeResources.xaml`.
6. **Editor UI** — add a `<local:ColorTokenRow Label="My New Token" HexValue="{x:Bind ViewModel.MyNewToken, Mode=TwoWay}" ... />` in `ThemeEditorPage.xaml`.
7. **ViewModel** — add the corresponding property in `ThemeEditorViewModel.cs` following the existing pattern.

That's it. No other files need changing.

---

## System Integration — Safety Boundaries

| Feature                  | API Used                                  | Risk    |
|--------------------------|-------------------------------------------|---------|
| Read accent color        | DWM registry (read-only)                  | None    |
| Read dark/light mode     | Personalize registry (read-only)          | None    |
| Set wallpaper            | `SystemParametersInfo` (documented Win32) | Low     |
| Set accent color         | DWM registry (write, known keys only)     | Medium* |
| Reset accent             | Delete the same known keys                | Medium* |

\* Always guarded behind the "Advanced / use at your own risk" toggle. No DLL injection. No undocumented kernel calls. Undo is always available via "Reset to Windows Default".

---

## License

MIT — free for personal and commercial use.
