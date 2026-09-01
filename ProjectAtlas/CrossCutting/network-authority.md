# Network authority map

Status: verified from current NGO code; adversarial multiplayer behavior not exhaustively tested  
Last verified: 2026-08-30

## Authority table

| Domain | Writer/decision owner | Client role | Main boundary |
|---|---|---|---|
| Session approval/capacity | Server `SessionService` | Supplies join request/code; receives result | NGO connection approval |
| Scene changes | Server | Follows NGO scene sync | `SessionService` + NGO SceneManager |
| Player spawn | Server | Owns assigned player object | `PlayerSpawner` |
| Player locomotion/state | Owning client for current movement variables | Non-owners render replicated state | `CharacterController2D` NetworkVariables |
| Machine occupancy | Server | Requests start/stop; accepted owner gets UI | `MachineInstance` RPCs/NetworkVariables |
| Cargo hold | Server | Owner proposes cursor/grab intent | `CargoGrabController` + `CargoHoldState` |
| Cargo physics/condition | Server | Presents replicated result | `CargoController`, `CargoHoldSolver`, modules |
| Ship/room resources | Server | Reads/presents, sends bounded action RPCs | `SubmarineManager`, `RoomMarker`, machine managers |
| Door/leak repair | Server | Requests interaction | `DoorConnection`, `WaterLeak`/`HullLeak` RPCs |
| Route seed/current node | Server | Reconstructs/presents graph | `MapNodeManager` NetworkVariables |
| Ship drive/map movement | Server | Active station owner sends input | `MapNetworkMovement.SubmitInputServerRpc` |
| Sonar blips | Local presentation/query | Scans local exterior physics | `AdvancedSonarSystem` |
| UI/camera/audio | Local client | Presentation and local input | managers/controllers |

## Trust rules

- A `[Rpc(SendTo.Server)]` attribute identifies the destination, not sufficient validation. Validate sender ownership/eligibility, machine occupancy, target identity, range, bounds, rate, and current state as applicable.
- `MachineInstance` is the shared “who may operate this station” gate. Machine result RPCs should additionally verify the sender is the current accepted player.
- Cargo grip validates contact/range/staleness on the server before state changes and applies physics on the server.
- Owner-written player movement is a deliberate responsiveness tradeoff. Do not reuse owner position alone as proof of authorization for valuable actions.
- Steam IDs carried in connection payloads are self-reported unless independently verified by the server/transport.

## Replication guidance

- Use server-write `NetworkVariable` for persistent authoritative state and targeted RPCs for one-client UI/events.
- Keep local presentation outside authoritative state unless another peer needs the information.
- Avoid sending raw high-frequency floats when the existing contract uses clamping, cadence, or quantization.
- Handle spawn/despawn/disconnect cleanup for occupied machines, held cargo, player hands, listeners, and static registries.
- Test host and remote client paths; host-only success can hide authority errors.

## Cross-system hotspots

Machine minigames, cargo gripping, route transitions, and room repairs all accept client-originated input that produces server gameplay effects. Review these whenever shared player identity, RPC permissions, or session teardown changes.

## Update this page when

Change a writer permission, RPC target/permission/validation, replicated variable, player ownership, session approval, server physics, or any local-vs-network boundary.
