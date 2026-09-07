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

## Next

The scene compositor will make Apply World a real desktop-wide operation. After that, VibeFinder/media context becomes a continuous signal that can morph a scene instead of a separate destination.
