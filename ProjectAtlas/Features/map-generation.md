# Map generation

Status: verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

Map generation builds the playable local map for the current route node using TileWorldCreator, guarantees a connected navigable result within retry limits, positions it relative to the submarine, and creates exits for child nodes. It does not choose the route graph; that is `MapNodeManager`.

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Map System/LevelGenarator/MapGenerate.cs` | Async TileWorld generation, retry/cancellation, placement, and exit creation. |
| `Assets/Script/Map System/LevelGenarator/ExitPoint.cs` | Player/ship transition boundary to the next node. |
| `Assets/Script/Map System/LevelGenarator/MapTestScript.cs` | Current attached generation helper/test control; not classified unused. |
| `Assets/Prefab/Managers/[MANAGER] MapGen.prefab` | `MapGenerate` + `MapTestScript` manager prefab. |
| `Assets/Prefab/MapLevel-Grid/Exit.prefab` | Generated exit marker/trigger. |
| `Assets/Scenes/MainLevel/MainMapConfig.asset` | Current TileWorld configuration. |

## Runtime flow

1. Navigation selects the current `MapData` from the route node.
2. `MapGenerate` cancels any previous generation and invokes TileWorldCreator asynchronously.
3. It retries seeds until it finds the required connected path or reaches configured failure bounds.
4. Tile layers/build output are generated, aligned to the ship/world movement frame, and old transition state is reset.
5. One exit is spawned/configured for relevant child nodes. Each receives a `RadarWaypoint` label so sonar can present it.
6. `ExitPoint` hands successful traversal back to navigation for node advancement/regeneration.

## Contracts and invariants

- Generation must be cancellable because node transitions can supersede in-flight work.
- An accepted layout must preserve a connected navigable route; changing blueprint layers must keep the connectivity test meaningful.
- Generated content is part of the moving-map frame managed by map movement, not the interior ship frame.
- Exit count/identity must correspond to available child nodes.
- `RadarWaypoint` setup is the bridge from generated exits to sonar.

## Dependencies

Consumes `MapData`/node selection and TileWorldCreator configuration. Supplies terrain/colliders/exits to map movement, collision, navigation, and sonar.

## Risks and unknowns

- The folder is misspelled `LevelGenarator`; paths in code/tools must use the actual name until deliberately migrated.
- Random retries need deterministic/server-consistent selection in multiplayer; the graph seed is networked, while generated visual/physics parity should be tested across host/client.
- `MapTestScript` is attached in current content despite its name. Remove only with scene/prefab evidence and a replacement control path.

## Update this page when

Change TileWorld configuration, async/cancellation behavior, seed/retry/connectivity rules, placement frame, exit spawning/transition, or map-generation prefab wiring.
