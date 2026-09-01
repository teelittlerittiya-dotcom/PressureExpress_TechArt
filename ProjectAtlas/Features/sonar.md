# Sonar

Status: verified with one unwired input type  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

Sonar scans exterior physics, renders transient contacts and persistent navigation waypoints, and optionally detects passive noise emitters. It presents information; route selection and ship movement remain owned by navigation.

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Ship System/SonarSystem/AdvancedSonarSystem.cs` | Active ray scan, passive overlap scan, auto-scan input, ping audio. |
| `Assets/Script/Ship System/SonarSystem/SonarUIController.cs` | Pooled blips and expanding scan ring. |
| `Assets/Script/Ship System/SonarSystem/SonarBlip.cs` | Individual contact visual lifecycle. |
| `Assets/Script/Ship System/SonarSystem/RadarWaypoint.cs` | Static waypoint registry, label, and setup contract. |
| `Assets/Script/Ship System/SonarSystem/SonarWaypointUIController.cs` | Resolves scanner/ship and projects/clamps waypoint UI. |
| `Assets/Script/Ship System/SonarSystem/WaypointUIElement.cs` | Pooled waypoint label/distance element. |
| `Assets/Script/Ship System/SonarSystem/NoiseSource.cs` | Passive sonar signal component; currently no asset attachment found. |
| `Assets/Prefab/Ship/Machine/Sonar.prefab` | Sonar station/presentation prefab. |

## Runtime flow

1. `AdvancedSonarSystem` emits a 360-degree set of physics raycasts for an active scan.
2. Hits are converted to pooled `SonarBlip` visuals and an expanding ring through `SonarUIController`.
3. Passive mode uses overlap queries and `NoiseSource` strength/range where such components exist.
4. `RadarWaypoint` instances register globally. Generated exits receive waypoints from `MapGenerate`.
5. `SonarWaypointUIController` projects each waypoint relative to scanner/ship, clamps off-screen points to the radar edge, and displays distance/label.

## Contracts and invariants

- Sonar queries the exterior map physics layers; layer-mask changes affect scan truth.
- `RadarWaypoint.Setup(label)` is the generation/navigation integration point.
- UI pooling avoids per-scan allocation; the current blip pool target is 500.
- Sonar is currently a local presentation/query system; do not assume a blip is server-validated gameplay state.

## Current wiring

`AdvancedSonarSystem` is present on `MainShip - 3D.prefab`. Navigation UI is registered as `MachineUIType.MapNavigation`. Generated exit prefabs gain `RadarWaypoint` during map generation.

## Risks and unknowns

- `AdvancedSonarSystem` searches for `NoiseSource`, but no current scene/prefab attachment was found. Keep the class as an unwired contract, not a deletion candidate.
- Physics layer/tag configuration is data outside the scripts and must be included in sonar verification.
- Space-key auto-scan input can conflict with drive/player input if contexts are not isolated.

## Update this page when

Change scan geometry/layers, active/passive behavior, pooling, waypoint registration/projection, generated-exit setup, audio, or network authority.
