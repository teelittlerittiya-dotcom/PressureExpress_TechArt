# Pressure Express system index

Last verified: 2026-08-30 against commit `bd86b016` and live Unity 6000.3.10f1.

## Route by task

| If the task mentions… | Read first | Usually also read |
|---|---|---|
| startup, managers, services, update loop | [Bootstrap & framework](Features/bootstrap-framework.md) | [Scenes & bootstrap](CrossCutting/scenes-and-bootstrap.md) |
| host, join, room code, Steam, NGO, Vivox, Discord | [Networking & Steam](Features/networking-steam.md) | [Network authority](CrossCutting/network-authority.md) |
| movement, swimming, ladders, player animation | [Player](Features/player.md) | [Physics & rendering](CrossCutting/physics-rendering.md) |
| cursor hand, grab, weighted carry, multiplayer holding | [Holding & hands](Features/holding-and-hands.md) | [Cargo](Features/cargo.md), [Network authority](CrossCutting/network-authority.md) |
| cargo data, damage modules, collider generation, polish | [Cargo](Features/cargo.md) | [Holding & hands](Features/holding-and-hands.md), [Data assets](CrossCutting/data-assets.md) |
| rooms, water, hull, leaks, doors, submarine state | [Ship core](Features/ship-core.md) | [Ship machines](Features/ship-machines.md) |
| oxygen, pressure, fuel, pump, coolant, machine minigame | [Ship machines](Features/ship-machines.md) | [Ship core](Features/ship-core.md), [UI & menus](Features/ui-and-menus.md) |
| procedural level, TileWorld, exits | [Map generation](Features/map-generation.md) | [Map nodes & navigation](Features/map-nodes-navigation.md) |
| route graph, node UI, drive, map movement | [Map nodes & navigation](Features/map-nodes-navigation.md) | [Map generation](Features/map-generation.md), [Sonar](Features/sonar.md) |
| scan, radar, waypoint, noise | [Sonar](Features/sonar.md) | [Map nodes & navigation](Features/map-nodes-navigation.md) |
| main menu, pause/session panel, settings, machine UI | [UI & menus](Features/ui-and-menus.md) | [Networking & Steam](Features/networking-steam.md) |
| tutorial phases, prompts, highlights, exit beacon | [Tutorial](Features/tutorial.md) | [UI & menus](Features/ui-and-menus.md), [Ship machines](Features/ship-machines.md) |
| music, SFX, occlusion, voice filter, camera | [Audio & camera](Features/audio-and-camera.md) | [Ship core](Features/ship-core.md) |
| scene/prefab/SO wiring | [Scenes & bootstrap](CrossCutting/scenes-and-bootstrap.md) | [Data assets](CrossCutting/data-assets.md) |
| tests, setup wizard, validator, prototype helper | [Editor tooling & tests](CrossCutting/editor-tooling-and-tests.md) | relevant feature page |
| head lure, bulb, speaking light, pixel shader | [Player](Features/player.md) | [Physics & rendering](CrossCutting/physics-rendering.md) |
| experimental prototype, isolated development harness | [Editor tooling & tests](CrossCutting/editor-tooling-and-tests.md) | relevant feature page |
| old, duplicate, legacy, unused | [Exclusions](EXCLUSIONS.md) | relevant feature page |

## Architectural spine

`Bootstrap.unity` creates persistent services → `SessionService` starts an NGO session → NGO loads `MainLevel.unity` → `PlayerSpawner` creates player objects → the player interacts with `MachineInstance` and cargo → ship, map, sonar, UI, audio, and tutorial systems consume those states.

The main shared boundaries are:

- `ServiceLocator` and selected singleton `Instance` properties for manager discovery;
- `UpdateManager` for custom update interfaces;
- NGO `NetworkVariable`/RPCs for replicated state;
- `MachineInstance` + `CanvasManager` + `MachineUIType` for station interaction;
- `SubmarineManager`/`RoomMarker` for the physical ship environment;
- ScriptableObjects for cargo, grip, map, room, and machine tuning.

## Known caution zones

- The main-menu/session UI is mid-migration from uGUI to UI Toolkit.
- Pressure exists in both `SubmarineManager` and `PressureSystemManager`; verify the intended authority before extending it.
- Several consumers search for components that no current prefab attaches (`FuelItem`, `PumpHandleVisualizer`, `NoiseSource`).
- `Assets/Scenes/Minigame.unity` has unresolved merge markers and is not a build scene.
- See [Exclusions](EXCLUSIONS.md) before deleting or documenting apparently old files.
