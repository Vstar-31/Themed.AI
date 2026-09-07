# Themed.AI Desktop Rehaul

Themed.AI is evolving from a theme manager into a visual desktop environment engine.

The desktop is the product: a scene can orchestrate theme, wallpaper, widgets, effects, Windows behavior, audio response, and VibeFinder context as one coherent state.

## Rehaul principles

- Visible changes first: every milestone should change the experience, not only add plumbing.
- Scenes are the unit of creativity: generate, save, share, automate and transition between complete desktop worlds.
- Vibe is an input to the whole desktop, not a standalone generator.
- Existing themes/widgets remain compatible while the scene layer orchestrates them.
- Windows integration is opt-in and failure-tolerant.
- Performance is part of the design: centralized scheduling, throttled sources and GPU-friendly effects.

## Implemented in this milestone

- Added persisted `DesktopScene` / world model above the existing theme + widget layers.
- Added atomic scene storage at `%LOCALAPPDATA%\\ThemedAI\\scenes.json`.
- Added `DesktopSceneService` with active-world state and serialized writes.
- Added a new visual Studio surface with world generation and selection.
- Added a Studio entry point to the main shell without removing existing pages.

## Next layers

The intended next slices are the desktop scene compositor, world-to-widget orchestration, Windows environment adapters, VibeFinder/media-driven adaptation, and a scene/package ecosystem.
