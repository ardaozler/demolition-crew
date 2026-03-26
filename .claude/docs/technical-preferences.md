# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6 (6000.3.8f1)
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline) 17.3.0
- **Physics**: PhysX (default) + RayFire (destruction)
- **Networking**: Netcode for GameObjects 2.10.0

## Naming Conventions

- **Classes**: PascalCase (e.g., `PlayerController`)
- **Public fields/properties**: PascalCase (e.g., `MoveSpeed`)
- **Private fields**: _camelCase (e.g., `_moveSpeed`)
- **Methods**: PascalCase (e.g., `TakeDamage()`)
- **Events/Signals**: PascalCase past tense (e.g., `OnHealthChanged`)
- **Files**: PascalCase matching class (e.g., `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase (e.g., `Player.prefab`, `SampleScene.unity`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60fps (currently drops to ~30fps — optimization needed)
- **Frame Budget**: 16.6ms target
- **Draw Calls**: [TO BE CONFIGURED]
- **Memory Ceiling**: [TO BE CONFIGURED]

## Testing

- **Framework**: Unity Test Framework (NUnit) 1.6.0
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

- **RayFire** — Destruction physics
- **PolygonConstruction** — Art asset pack
- **Netcode for GameObjects** — Multiplayer networking
- **Unity Multiplayer Services** — Lobby/matchmaking
- **Vivox** — Voice chat

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]
