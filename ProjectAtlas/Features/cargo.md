# Cargo

Status: verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

Cargo is a one-prefab, data-driven 2.5D physics system. It owns cargo identity/configuration, server runtime condition, generated 3D compound colliders, environmental sensing, condition modules, presentation polish, debug inspection, and cargo UI. Physical player holding is documented separately.

## Canonical files

| Path | Role |
|---|---|
| `Assets/Script/Cargo System/CargoController.cs` | Network object and authoritative runtime-state/module coordinator. |
| `Assets/Script/Cargo System/CargoItemData.cs` | Cargo ScriptableObject identity/base configuration. |
| `Assets/Script/Cargo System/CargoRuntimeState.cs` | Replicated/current condition values. |
| `Assets/Script/Cargo System/CargoColliderBuilder.cs` | Converts sprite physics shapes to 3D convex compound colliders. |
| `Assets/Script/Cargo System/CargoProximitySensor.cs` | Resolves containing room/environment inputs. |
| `Assets/Script/Cargo System/CargoModule.cs` | Module data contract. |
| `Assets/Script/Cargo System/CargoModuleBase.cs` | Runtime module base. |
| `Assets/Script/Cargo System/ImpactModule.cs` | Impact condition/damage. |
| `Assets/Script/Cargo System/PressureModule.cs` | Pressure exposure. |
| `Assets/Script/Cargo System/RottenModule.cs` | Freshness/rot progression. |
| `Assets/Script/Cargo System/TemperatureModule.cs` | Temperature exposure. |
| `Assets/Script/Cargo System/Presentation/CargoPolishController.cs` | Feedback/presentation coordinator. |
| `Assets/Script/Cargo System/Presentation/CargoPolishProfile.cs` | Presentation tuning asset. |
| `Assets/Script/Cargo System/CargoDebugMode.cs` | Runtime hover/debug inspection. |
| `Assets/Script/Rendering/SpriteRenderOrderPolicy.cs` | Shared 2.5D sprite sorting rules. |
| `Assets/Prefab/Cargo/CargoController (new).prefab` | Canonical cargo prefab. |

## Runtime flow

1. A cargo instance uses `CargoItemData` plus module assets rather than a unique behavior prefab.
2. `CargoColliderBuilder` reads the sprite physics shape and creates convex 3D child colliders suitable for XY-plane physics and gripping.
3. On the server, `CargoController` initializes runtime condition and module logic.
4. `CargoProximitySensor` samples room water, pressure, and temperature. Impact callbacks feed impact modules.
5. Modules mutate condition/state under server authority. Network state supplies clients with presentation/UI values.
6. `CargoPolishController`, particles, and UI render those values without becoming gameplay authority.
7. `CargoHoldSolver` receives validated holders from the holding system and applies server physics forces.

## Data and prefab wiring

`Assets/Data/Cargo/` contains Eggs, Nuke, prototype, test-variant, polish, and `_unfinished` assets. `_unfinished` means content status, not unused-code status. Current cargo prefab components are `CargoController`, `CargoColliderBuilder`, `CargoHoldSolver`, `CargoProximitySensor`, `CargoPolishController`, and `ParticleManager`.

Cargo UI lives in `Assets/Script/Cargo System/UI/` and prefabs under `Assets/Prefab/Cargo/CargoUI/`. See [Data assets](../CrossCutting/data-assets.md) before adding a new cargo type.

## Authority and invariants

- Gameplay state and rigidbody forces are server-authoritative.
- All cargo variants should use the canonical prefab and data/modules; avoid per-item behavior-prefab forks.
- Runtime physics is 3D on an XY plane. Do not reintroduce 2D colliders/rigidbodies.
- Generated collider geometry is part of the holding contact contract.
- Presentation feedback may observe condition but must not change it directly.

## Tests and tooling

Cargo edit/play mode tests live under `Assets/Tests/`. Validators and migration tools live under `Assets/Editor/`: prototype validation, polish validation/migration, 2D-to-3D conversion, and weighted-holding validation.

## Dependencies

Consumes `RoomMarker`/ship environment and NGO. Integrates with holding, particles, spatial rendering, and cargo UI. Map/contract delivery systems can consume cargo identity/state but should not bypass `CargoController`.

## Risks and unknowns

- Asset sets include explicit prototypes and unfinished content; selecting “all CargoItemData” at runtime may include content not ready for production.
- Collider generation, sprite import physics shapes, and grip contact must be changed/tested together.
- Existing plan documents contain richer design history, but are not current-runtime authority.

## Update this page when

Change cargo prefab composition, data/module contracts, runtime state/authority, collider generation, room sensing, condition math, polish behavior, sorting, UI contracts, or cargo asset layout.
