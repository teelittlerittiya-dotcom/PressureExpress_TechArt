# Suspected unused, legacy-only, and unwired code

Last verified: 2026-08-30. No file in this page was deleted. This is an investigation aid, not deletion approval.

## Meaning of labels

- **Suspected unused**: no first-party code reference, serialized GUID attachment, live component, or current asset dependency was found, and an active replacement/overlap exists.
- **Legacy-only**: references exist only in an old prefab or non-build scene.
- **Unwired watch**: code is part of a current system or explicitly searched for by a consumer, but no current asset attachment was found. These files remain routed in `SYSTEMS.json`.

The audit checked C# references, serialized GUID references, Unity AssetDatabase dependencies, enabled Build Settings scenes, live `MainLevel` components, and key prefab component inventories. Reflection, runtime `AddComponent`, addressables, custom asset loading, or future scenes can invalidate a conclusion.

## Suspected unused

| File | Evidence and likely replacement |
|---|---|
| `Assets/Script/_Interface/IPlayerInteractable.cs` | No implementer/caller or serialized use found. Current station interaction is owned by `MachineInstance`. |
| `Assets/Script/Player/Hand Handle/PlayerHandController.cs` | No attachment/reference found. Current path is `PlayerHand` + `CursorIntentProvider` + `CargoGrabController`. |
| `Assets/Script/Player/PlayerAnimationTrigger.cs` | No attachment/reference found; current player controller directly drives animation state. |
| `Assets/Script/Player/PlayerController(Nomal Networktranform)/PlayerController.cs` | Defines the older `CharacterController2D_1`; no attachment/reference found. `CharacterController2D.cs` is on `Player.prefab`. |
| `Assets/Script/Audio/Footstep.cs` | No attachment found. `CharacterController2D` contains the current spatial footstep logic. |
| `Assets/Script/UI/Setting & Main Menu/SessionPanel.cs` | No current attachment found. UI Toolkit `SessionPanelView` is the replacement implementation, although that replacement is not yet assigned to `GameBootstrap.sessionUIPrefab`. |
| `Assets/Script/Ship System/ShipMachine/MinigameLogic/WaterPumpController.cs` | No attachment/caller found. Active pump path uses `PumpMachine` + `DrainPumpMinigame`. |
| `Assets/Script/Ship System/ShipMachine/O2_Machine/FuelOxygen.cs` | No attachment/caller found. Fuel and oxygen are owned by `FuelSystemManager` and `OxygenSystemManager`. |
| `Assets/Script/Ship System/ShipResources/ResourceManager.cs` | No current dependency/attachment found; overlaps the active ship system managers. |
| `Assets/Script/Ship System/ShipResources/ResourceDebugUI.cs` | No current dependency/attachment found; tied to the unused resource-manager path. |
| `Assets/Script/Ship System/ShipMachine/MachineData/OxygenMachineData.cs` | No current asset dependency found; part of the old resource-data cluster. |
| `Assets/Script/Ship System/ShipMachine/MachineData/PowerMachineData.cs` | No current asset dependency found; part of the old resource-data cluster. |
| `Assets/Script/Ship System/ShipMachine/MachineData/PressureMachineData.cs` | No current asset dependency found; part of the old resource-data cluster. |
| `Assets/Script/Ship System/ShipMachine/MachineData/TemperatureMachineData.cs` | No current asset dependency found; part of the old resource-data cluster. Keep the active base `MachineData.cs`. |

## Legacy-only

| File | Legacy reference |
|---|---|
| `Assets/Script/Ship System/ShipMachine/MinigameLogic/DrainPump.cs` | Serialized only in `Assets/Scenes/Minigame.unity`; the scene is not in Build Settings and currently contains unresolved merge-conflict markers. |
| `Assets/Script/Ship System/ShipMachine/MinigameLogic/Fuel.cs` | Serialized only in the same invalid, non-build `Minigame.unity` scene. |
| `Assets/Script/Ship System/ShipMachine/MinigameLogic/FuelItemData.cs` | Data contract for the legacy `Fuel` minigame path. |
| `Assets/Script/Player/Platform Handle/PlatformController.cs` | Used only by `Assets/Prefab/Ship/-old/Platfrom/Platform.prefab`. |
| `Assets/Script/Player/Platform Handle/PlatformUnit.cs` | Used only by the same `-old` platform prefab. |

## Unwired watch — do not classify as unused

| File/system | Current uncertainty |
|---|---|
| `Assets/Script/Menu/QQuit.cs` | No current component GUID attachment, but old UI prefabs retain dangling `Quit` UnityEvent type metadata. `MainMenu.unity` still has old `MainMenuCanva` active while new `UI_MainMenu` is inactive. |
| `Assets/Script/Ship System/SonarSystem/NoiseSource.cs` | `AdvancedSonarSystem` actively searches for this type, but no current asset attaches it. |
| `Assets/Script/Ship System/ShipMachine/FuelMachine/FuelItem.cs` | `FuelConverterMachine` expects it, but no current prefab attaches it. |
| `Assets/Script/Ship System/ShipMachine/O2_Machine/PumpHandleVisualizer.cs` | `OxygenMachineController` searches for it, but no current prefab attaches it. |
| `Assets/Script/UI/Toolkit/SessionPanelView.cs` | Replacement implementation exists and its prefab exists, but `Bootstrap.unity` currently leaves `GameBootstrap.sessionUIPrefab` null. |
| `Assets/Script/Ship System/ShipMachine/PressureMachine/PressureSystemManager.cs` | Active component, but pressure ownership overlaps `SubmarineManager`; this is an architecture caution, not unused-code evidence. |

Test- or prototype-named scripts are not automatically excluded. `MapTestScript`, `TestUI`, and `DrawController` are present in current build/live content and remain documented.
