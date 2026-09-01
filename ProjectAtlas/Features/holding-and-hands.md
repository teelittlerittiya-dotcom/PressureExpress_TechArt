# Holding and hands

Status: verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

This system turns local cursor intent into a server-authoritative, replicated cargo grip. It owns reach/contact validation, per-player hold state, the visible networked hand, multi-holder force application, and grip tuning. Cargo condition/damage remains owned by [Cargo](cargo.md).

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Player/CargoGrabController.cs` | Local intent/hover and server RPC validation; one replicated hold state per player. |
| `Assets/Script/Player/Hand Handle/PlayerHand.cs` | Spawned network hand, owner cursor/preview, peer-visible grip state and placement. |
| `Assets/Script/Player/Holding/CursorIntentProvider.cs` | Mouse-to-plane conversion and virtual-hand reach clamp. |
| `Assets/Script/Player/Holding/CargoHoldState.cs` | Compact replicated cargo/point/intent state. |
| `Assets/Script/Player/Holding/GripConfiguration.cs` | ScriptableObject tuning contract. |
| `Assets/Script/Player/Holding/GripForceModel.cs` | Mass-independent force/torque helpers, clamping, and quantization. |
| `Assets/Script/Player/Holding/GripContactUtility.cs` | 3D collider contact/penetration validation. |
| `Assets/Script/Cargo System/CargoHoldSolver.cs` | Collects active holders and applies forces to cargo. |
| `Assets/Prefab/Player/PlayerHand.prefab` | Networked hand visual/query object. |
| `Assets/Data/Holding/Default Grip Configuration.asset` | Current shared tuning asset. |

## Runtime flow

1. The owning client projects cursor input onto the gameplay plane and clamps intent to the configured radius.
2. Hover is local presentation. A grab/release request is sent through `CargoGrabController`.
3. The server checks target identity, distance/contact, holder limits, and stale intent before updating that player's `CargoHoldState`.
4. All peers render the network hand and held state. The owner additionally receives cursor/preview behavior.
5. `CargoHoldSolver` gathers valid controllers holding a cargo object and applies `AddForceAtPosition` using the shared force model. Multiple holders contribute without changing the configured feel merely because cargo mass changed.
6. Release, invalid contact, despawn, or stale input clears the hold.

## Authority and invariants

- The server decides whether a hold exists; clients only propose intent.
- Contact is validated with 3D collider geometry. The hand rigidbody/trigger is a query/visual aid, not the authority.
- The cargo rigidbody receives forces on the server. Replication presents the resulting motion.
- Reach, maximum force/speed, send cadence, quantization, stale timeout, and maximum holders come from `GripConfiguration`.
- Holding must stay on the XY gameplay plane and share the cargo collider built by `CargoColliderBuilder`.

## Current prefab wiring

`Player.prefab` carries `CargoGrabController` and `CursorIntentProvider`; `PlayerHand.prefab` carries `PlayerHand`; `CargoController (new).prefab` carries `CargoHoldSolver`. The prior `PlayerHandController` path is a suspected unused replacement candidate, not an active dependency.

## Tests and tooling

- `Assets/Tests/EditMode/WeightedHoldingEditModeTests.cs`
- `Assets/Tests/PlayMode/WeightedHoldingPlayModeTests.cs`
- `Assets/Editor/WeightedHoldingValidator.cs`

## Risks and unknowns

- Authority depends on frequent enough intent updates and strict stale/range validation; tune all related values together.
- Changes to generated cargo colliders can change contact behavior even when holding code is untouched.
- Never document `PlayerHandController` as the active hand without new wiring evidence.

## Update this page when

Change grip RPCs/state, validation, force math, hand spawning/presentation, holder aggregation, grip configuration fields, or player/cargo prefab composition.
