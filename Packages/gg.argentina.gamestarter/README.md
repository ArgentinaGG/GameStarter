# GameStarter

GameStarter supplies common functionality for new games without duplicating those systems in every project.

## Module structure

- `Runtime/Modules/`: reusable runtime features, organized by responsibility rather than a generic `Scripts` folder.
- `Runtime/Modules/Localization/`: localization configuration and cached JSON key lookups.
- `Runtime/Modules/Menu/`: shared menu behavior and UI assets when implemented.
- `Editor/`: editor-only package hub and tooling, kept separate from runtime code.
- `Samples~/Localization/`: source JSON examples copied into a game project by the package hub.