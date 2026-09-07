# Themed.AI Desktop Rehaul

Themed.AI is evolving from a theme manager into a visual desktop environment engine.

The desktop is the product: a scene can orchestrate theme, wallpaper, widgets, effects, Windows behavior, audio response, and VibeFinder context as one coherent state.

## Rehaul milestone 1

Implemented:
- Persisted `DesktopScene` / world model above themes and widgets.
- Atomic scene storage at `%LOCALAPPDATA%\\ThemedAI\\scenes.json`.
- `DesktopSceneService` with active-world state and serialized writes.
- New visual Studio surface with world generation and selection.
- Studio entry point in the main shell.

## Why this exists

The old architecture exposed individual capabilities (themes, widgets, automation, VibeFinder) but did not have a first-class object tying them together. A scene is that missing orchestration layer.

## Next milestone

Build the scene compositor and apply pipeline so `Apply world` visibly changes the desktop: wallpaper, theme, widget layout and supported Windows presentation state should transition together. After that, VibeFinder/media context becomes a live input to scenes rather than a separate destination.
