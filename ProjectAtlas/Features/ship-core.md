# Ship core

Status: verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

Ship core owns the submarine-wide physical/environment simulation: rooms, water, temperature, pressure, oxygen aggregation, ballast/depth consequences, doors, hull leaks, collisions, alarms, health states, and terminal failure. Machine controls feed this simulation but are documented separately.

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Ship System/ShipRoom/SubmarineManager.cs` | Server-oriented global simulation, replicated ship values, state machine, and failure. |
| `Assets/Script/Ship System/ShipRoom/RoomMarker.cs` | Per-room water/temperature/pressure/HP/leak state and cargo containment. |
| `Assets/Script/Ship System/ShipRoom/DoorConnection.cs` | Connects two rooms; server-owned open state and flow path. |
| `Assets/Script/Ship System/ShipRoom/HullLeak.cs` | Leak source/damage behavior. |
| `Assets/Script/Ship System/ShipRoom/SubmarineCollision.cs` | Converts map collisions into ship consequences. |
| `Assets/Script/Ship System/ShipRoom/SubmarineStates.cs` | Normal/warning/critical/failure state implementations. |
| `Assets/Script/Ship System/ShipRoom/RoomWaterVisualizer.cs` | Water-surface presentation and shared water-height query. |
| `Assets/Script/Ship System/ShipRoom/RoomTypeSO.cs` | Room type/configuration asset. |
| `Assets/Prefab/Ship/MainShip - 3D.prefab` | Canonical assembled ship. |
| `Assets/Prefab/Ship/Rooms/Room.prefab` | Canonical room building block. |

## Runtime flow

1. `MainLevel` contains the assembled ship and `SubmarineManager`, with ten room markers in the current prefab.
2. The server registers `SubmarineManager` and rooms with `UpdateManager` and initializes the submarine state machine.
3. Rooms simulate water, pressure, temperature, integrity, and leaks. Doors determine inter-room equalization/flow.
4. The manager aggregates room conditions into ship oxygen, temperature, pressure, depth/ballast, critical state, and failure state.
5. Machines add/remove fuel, oxygen, water, temperature, or pressure effects; map movement supplies depth and collision input.
6. NetworkVariables expose authoritative values to clients; visualizers, audio, UI, cargo, and camera read them.

## Authority and contracts

- Ship-condition mutation belongs on the server. Clients may request door/machine actions through RPCs.
- `SubmarineManager.Instance` is the global ship source; `RoomMarker` is the per-room source.
- `DoorConnection.isOpen` is server-written and controls room/audio connectivity.
- `RoomMarker.ContainsPoint` and water-surface queries are cross-system spatial contracts used by cargo, audio, and camera.
- State-machine transitions and game-over behavior consume the aggregate simulation; avoid duplicating critical thresholds in UI.

## Current prefab wiring

Live inspection of `MainShip - 3D.prefab` found `SubmarineManager`, `SubmarineCollision`, ten room/water component sets, active machine systems, sonar, SFX, and camera overrides. `Assets/Prefab/Ship/Rooms/[Room]RoomManeger.prefab` also assembles ten rooms plus `SubmarineManager` and should be treated as a source/variant until its relationship to the main ship is intentionally consolidated.

## Dependencies

Consumes map movement/depth/collision and machine outputs. Supplies environment state to cargo, spatial audio, underwater camera/voice, tutorial, alarms, UI, and failure flow.

## Risks and unknowns

- `PressureSystemManager` overlaps pressure ownership with `SubmarineManager`; verify which value a new consumer needs.
- Tutorial code intentionally changes normal setup/flow; the manager has tutorial guards.
- Room graphs depend on correctly assigned room and door references. A visual door with missing endpoints also breaks audio occlusion.

## Update this page when

Change room/environment simulation, replicated ship values, state transitions, door/leak/collision behavior, water spatial queries, room count/prefabs, or ship-manager ownership.
