# World package workflow

Desktop World offers Import world and an Export button on each scene. Packages use .themedworld and contain world.json, ordered nested .themedwidget files, and an optional raster wallpaper. Widget images are bundled by the existing widget package service. A palette snapshot is embedded and used by Studio on explicit apply; duplicating a world preserves it.

Imports receive new world, widget and palette identities. Widgets remain disabled until the world is applied. Imported worlds do not become startup fallbacks or automatic vibe-switch targets. Import never replaces existing worlds or selects an active world. Failed inventory saves remove newly imported assets, while preserving assets if persistence succeeded and a subsequent event handler failed.

Limits: 32 placements, 1 MiB manifest, 64 MiB package and cumulative expanded nested widget assets, 16 MiB wallpaper. Unknown entries, duplicate entries, symlinks, traversal names, missing widgets, unsupported scene versions and invalid placements are rejected. Each nested widget is validated by SkinPackageService. Exports use a temporary file and only replace the destination after success. Weather, VibeFinder, Web/JSON and plugin connection settings follow the existing widget export filtering. Custom text, arbitrary links, effect parameters, palette tokens and variables require author review before sharing.

World packages preserve stored placement snapshots; if a legacy placement lacks one, export resolves its widget from the current inventory. Monitor names and coordinates are preserved, so imported layouts may need repositioning. Imports install assets under the isolated AppDataPaths root. Deleting a world does not yet garbage-collect its assets. Package operations currently have no progress cancellation.

Validation: 801 Release tests passed, including 11 world-package cases covering round-trip identities/layout/palette, images/wallpaper/credential filtering, unexpected/duplicate/traversal entries, unsupported versions, atomic export failure, partial-import cleanup, failed inventory writes, and inactive restart until explicit selection. WinUI Release compilation passed with zero warnings/errors. Final UI routing was rebuilt after switching to the tested import-and-save operation. GUI picker and live world-application acceptance remain pending; no desktop acceptance is claimed for this feature.

The SDK policy now permits newer .NET 10 feature bands because Visual Studio replaced the pinned 10.0.204 SDK with 10.0.403 locally; the minimum pinned SDK version remains 10.0.204.
