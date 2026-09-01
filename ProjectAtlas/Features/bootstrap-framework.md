# Bootstrap and framework

Status: verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

This layer creates the persistent application services, selects the networking environment, provides lightweight service/update infrastructure, and exposes reusable state-machine and ship-system bases. It does not own a multiplayer session after startup; that belongs to `SessionService`.

## Canonical files

| Path | Role |
|---|---|
| `Assets/Script/GameBootstrap.cs` | Sole persistent startup composition root. |
| `Assets/Script/Framework/ServiceLocator.cs` | Type-keyed registration and discovery for selected managers. |
| `Assets/Script/Framework/UpdateManager.cs` | Dispatches custom update interfaces and is created dynamically by the bootstrap. |
| `Assets/Script/Framework/ShipSystemBase.cs` | NGO base for server-owned ship resource values. |
| `Assets/Script/Framework/StateMachine.cs` | Generic current-state lifecycle. |
| `Assets/Script/Framework/StateMachineFactory.cs` | Builds the submarine state graph. |
| `Assets/Script/Framework/IState.cs` | State lifecycle contract. |
| `Assets/Script/Framework/AlarmUnit.cs` | Alarm presentation helper. |
| `Assets/Script/Framework/DisplaySettings.cs` | Shared resolution/fullscreen/volume persistence used by both menu generations. |
| `Assets/Script/Framework/UnityServicesBootstrap.cs` | Unity Gaming Services initialization path. |
| `Assets/Script/Analytic/AnalyticManager.cs` | Optional persistent analytics manager. |

## Runtime flow

1. `Bootstrap.unity` instantiates `GameBootstrap` once and marks its object persistent.
2. In the Editor it selects the local Unity Transport prefab; a player build initializes Steam with a timeout and selects the Facepunch/Steam prefab when ready.
3. It creates exactly one `NetworkManager`, then persistent `SteamService`, `SessionService`, and optional debug, analytics, Discord, Vivox, and session-UI objects.
4. It ensures an `UpdateManager` exists and loads `MainMenu`.
5. Feature managers register themselves with `ServiceLocator` or `UpdateManager`; the bootstrap does not simulate gameplay.

## Contracts and invariants

- Only one `NetworkManager` may survive startup. Network-prefab choice must precede creation of `SessionService`.
- `IUpdateable`, `IFixedUpdateable`, and `ILateUpdateable` are explicit registration contracts, not automatic MonoBehaviour discovery.
- `ShipSystemBase` values are server-written and client-readable unless a derived system documents otherwise.
- Singleton fields and `ServiceLocator` coexist; preserve registration/unregistration symmetry when changing a manager.

## Unity wiring

- Entry scene: `Assets/Scenes/Bootstrap.unity`.
- Network prefabs: `Assets/Prefab/Bootstrap/[Local] NetworkManager.prefab` and `[Steam] NetworkManager.prefab`.
- Service prefabs: `Assets/Prefab/Bootstrap/SteamService.prefab`, `SessionService.prefab`, and `NetworkDebugOverlay.prefab`.
- Optional application prefabs live under `Assets/Prefab/AppicationManager/`.
- `GameBootstrap.sessionUIPrefab` is currently null in `Bootstrap.unity`; see [UI and menus](ui-and-menus.md).

## Dependencies

Bootstrap calls into Steam/network/session services and loads the menu. Most gameplay features depend on its `NetworkManager`, `UpdateManager`, and service instances. Scene order and persistence are summarized in [Scenes and bootstrap](../CrossCutting/scenes-and-bootstrap.md).

## Risks and unknowns

- There is a misspelled `Assets/Scenes/Boostrap.unity`; Build Settings use the correctly spelled scene.
- Optional managers may be absent by prefab configuration, so consumers must tolerate null services where documented.
- Initialization ordering is sensitive: adding scene-local duplicate singletons can silently compete with persistent instances.

## Update this page when

Change startup order, environment selection, persistent managers, service discovery, update interfaces, base ship-system authority, or bootstrap prefab fields.
