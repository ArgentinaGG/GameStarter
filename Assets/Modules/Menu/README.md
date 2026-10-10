# Starter Menu Module

This folder reserves the starter-owned menu boundary. It currently contains documentation only; no menu scene or UI has been created.

## Intended responsibilities

- A ready-to-run main menu scene composed from reusable package components.
- Starter defaults for Play, Options, language selection, and a platform-appropriate Quit action.
- Game-owned layout, branding, translation content, configuration, and gameplay entry point.

Keep feature content together. Add scene, prefab, and other asset subfolders when those assets are implemented; a generic `Scripts` folder is not required.

Reusable functionality belongs in `Packages/gg.argentina.gamestarter/Runtime/Modules/Menu/`. Customize it through project-owned composition, configuration, or prefab variants rather than editing shared package assets.

When starting a new game from this project, this module becomes game-owned content and can be modified freely. Starter changes do not automatically propagate to existing games.
