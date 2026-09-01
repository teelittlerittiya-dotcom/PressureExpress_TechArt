# Map nodes and navigation

Status: verified with UI prototype caveat  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

This system creates the seeded route graph, selects the current node, renders the node map, drives the moving exterior world from station input, consumes fuel, detects obstacles, tracks depth, and coordinates transitions. Procedural tile construction is delegated to map generation.

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Map System/MapNodeGenerator/MapNodeManager.cs` | Network seed/current-node owner and in-memory graph construction. |
| `Assets/Script/Map System/MapNodeGenerator/MapNode.cs` | Generic graph node/parent-child structure. |
| `Assets/Script/Map System/MapNodeGenerator/MapData.cs` | Node content, prefab/type, water temperature, and water pressure. |
| `Assets/Script/Map System/MapNodeGenerator/MapDifficultySetting.cs` | Depth, count, branching, and node-type chances. |
| `Assets/Script/Map System/MapNodeGenerator/UI/MapUIDisplayManager.cs` | Builds the layered node UI at runtime. |
| `Assets/Script/Map System/MapNodeGenerator/UI/UIMapConnector.cs` | Draws parent/child connectors. |
| `Assets/Script/Map System/MapLevelNavigation/MapNetworkMovement.cs` | Server-authoritative exterior movement/input/collision/depth. |
| `Assets/Script/Map System/MapLevelNavigation/MapMoveController.cs` | Movement coordinator/input bridge. |
| `Assets/Script/Map System/MapLevelNavigation/MapNavigationMachine.cs` | Station `MachineInstance` that opens drive UI. |
| `Assets/Script/Map System/MapLevelNavigation/NavigationGameManager.cs` | Current-node and map-transition coordinator. |

## Graph flow

1. The server owns a network seed and current node selection in `MapNodeManager`.
2. It constructs an in-memory `MapNode<MapData>` graph from `MapDifficultySetting` and configured `MapData` pools, including branches and destinations.
3. `MapUIDisplayManager` creates node-slot UI and `UIMapConnector` relationships from that graph.
4. `NavigationGameManager` selects/advances a node and requests matching procedural content from `MapGenerate`.

## Driving flow

1. A player acquires `MapNavigationMachine`; `CanvasManager` opens the ship-drive UI.
2. `ShipDriveMinigameUI` sends input through `MapNetworkMovement.SubmitInputServerRpc`.
3. The server moves the exterior map relative to the stationary/interior ship frame, checks obstacle overlap, updates depth, and reports collision to `SubmarineCollision`.
4. Movement consumes fuel through `MachineManager`/`FuelSystemManager`.
5. Reaching an `ExitPoint` selects the corresponding child node and regenerates content.

## Authority and invariants

- Route seed/current node, drive input acceptance, exterior physics movement, collision effects, and fuel use are server-owned.
- Clients should not create divergent node graphs from non-network random state.
- The ship/interior and exterior map use different movement frames; world-space features must identify which frame owns them.
- `MapData.waterTemperature`/`waterPressure` are environment inputs for the selected node.

## Current wiring

Manager prefabs under `Assets/Prefab/Managers/` exist for Map Node, MapGen, MapMoveController, and NavigationGameManager. Node data assets live in `Assets/Script/Map System/MapNodeGenerator/Map Data/`. `MapNodeSlotUI.prefab` is under `Assets/Prefab/UI/Machines/_for_sonar/`.

## Risks and unknowns

- `UIMapConnector` contains prototype/incomplete comments; confirm visual behavior before relying on it as a finished map UI contract.
- Map movement, generated colliders, collision damage, and fuel use are tightly coupled and need a multiplayer play-mode pass after physics changes.
- Several development map scenes are not enabled build scenes.

## Update this page when

Change graph generation, node data/difficulty, current-node authority, node UI, drive RPC/input, exterior movement frame, collision/depth/fuel integration, or transitions.
