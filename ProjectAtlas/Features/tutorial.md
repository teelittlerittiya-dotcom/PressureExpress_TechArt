# Tutorial

Status: verified at code/prefab level; full runtime sequence not exercised  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

The tutorial sequences required machine interactions, introduces the sonar/navigation station, spawns an obstacle and exit beacon, guides the player with world/UI highlights and camera previews, and returns to completion/menu flow. It observes normal systems instead of maintaining separate tutorial-only machine implementations.

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Tutorial/TutorialManager.cs` | Tutorial phase/state owner and machine-completion registry. |
| `Assets/Script/Tutorial/TutorialMinigameOverlay.cs` | Per-machine multi-page dialogue and control highlights. |
| `Assets/Script/Tutorial/TutorialWorldHighlight.cs` | World marker/glow filtered by machine type and phase. |
| `Assets/Script/Tutorial/TutorialCameraPreview.cs` | Timed station tour using external camera control. |
| `Assets/Script/Tutorial/UI/TutorialTaskTrackerUI.cs` | Task rows, progress, phase feedback. |
| `Assets/Script/Tutorial/TutorialExitBeacon.cs` | Completion trigger, feedback, and menu fallback. |
| `Assets/Prefab/Tutorial/TutorialExitBeacon.prefab` | Spawned exit/completion asset. |
| `Assets/Scenes/Development/Tutorial.unity` | Enabled tutorial build scene. |

## Phase flow

1. `InternalMachines`: a configured set of `MachineUIType` tasks is pending. Successful minigames call `TutorialManager.ReportMachineCompleted`.
2. When all internal tasks complete, the manager enters `SonarStation`, enables/introduces navigation, and spawns a practice obstacle under the map-movement transform.
3. Successful Map Navigation reports completion and advances to `SteerToExit`.
4. The manager spawns an exit prefab ahead of the ship, ensures it has `RadarWaypoint`, and labels it `EXIT BEACON`.
5. Reaching `TutorialExitBeacon` triggers victory feedback and `FinishTutorial`, then exits through the configured/fallback menu flow.

## Contracts and invariants

- `MachineUIType` is the shared task identity between normal minigames, world highlights, task UI, and tutorial state.
- Normal machine result paths report completion; tutorial code should not grant resource effects directly as a substitute.
- Tutorial-spawned exterior objects must be parented to the moving map frame.
- Camera preview temporarily calls `MainCamController.SetExternalControl`; it must restore normal control on completion/skip/disable.
- The tutorial scene is a build scene and is intentionally allowed to use setup values different from `MainLevel`.

## Dependencies

Consumes `CanvasManager` machine-open events, machine completion calls, map movement, radar waypoints, main camera control, Feel feedback, TextMesh Pro/Text Animator, and return-to-menu/session logic.

## Risks and unknowns

- Full start-to-finish behavior was not played during this audit; serialized target lists and UI row assignments should be checked in Unity when editing phases.
- Scene setup can drift from the machine enum/registry. `TutorialSceneSetup.cs` is editor assistance, not runtime truth.
- Tutorial-specific obstacle/exit positioning assumes the current exterior movement axis/frame.

## Update this page when

Change phases/tasks, machine completion reporting, spawned obstacle/exit behavior, highlights/overlay/camera tour, tutorial scene wiring, or completion/menu flow.
