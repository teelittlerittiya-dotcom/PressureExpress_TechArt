# Ship machines

Status: verified with wiring cautions  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

Ship machines translate one-player-at-a-time station interaction and minigame results into server-authoritative fuel, oxygen, pressure, temperature, water, ballast, and navigation effects. `MachineInstance` is the shared acquisition/UI boundary; `CanvasManager` owns local UI instances.

## Shared contracts

| Path | Role |
|---|---|
| `Assets/Script/_Interface/MachineInstance.cs` | Network interaction lock, requesting player, targeted open/close RPCs, and UI type. |
| `Assets/Script/Ship System/ShipMachine/MachineManager.cs` | Service-located façade for fuel/oxygen and movement fuel use. |
| `Assets/Script/Ship System/ShipMachine/MinigameLogic/MinigameBaseUI.cs` | Common minigame lifecycle. |
| `Assets/Script/UI/MachineUIType.cs` | Stable registry key: Fuel, Oxygen, Pressure, Coolant, Water Pump, Map Navigation. |
| `Assets/Script/UI/CanvasManager.cs` | Maps `MachineUIType` to prefab and owns the current local UI. |

`MachineInstance.isUsing` and `currentPlayerId` are server-written. A player requests acquisition; the server rejects contention/invalid requests, targets UI open to the accepted owner, and releases on close/disconnect/exit. UI locks player movement locally but does not pause multiplayer time.

## Active machine families

| Family | Gameplay owner | UI/minigame | Output |
|---|---|---|---|
| Fuel | `FuelConverterMachine`, `FuelSystemManager`, `IFuelSource` | `FuelConverterMinigameUI`, `FuelLevelUI` | Adds/consumes server fuel; ship movement consumes through `MachineManager`. |
| Oxygen | `OxygenMachineInstance`, `OxygenMachineController`, `OxygenSystemManager` | `OxygenMachineMinigameUI` | Generates ship oxygen and can drain ballast while boosting. |
| Pressure | `PressureMachine`, `PressureSystemManager` | `PressureMinigameUI` | Submits timing result and adjusts pressure path. |
| Coolant | `CoolantMachine` | `CoolantMinigameUI` | Changes ship temperature. |
| Pump | `PumpMachine`, `WaterSystemManager`, `WaterLeak` | `DrainPumpMinigame` | Drain/fill mode changes room water or ballast; leaks are server-fixed. |
| Navigation | `MapNavigationMachine` | `ShipDriveMinigameUI` | Sends drive input to `MapNetworkMovement`; see map navigation page. |

Machine implementation folders are under `Assets/Script/Ship System/ShipMachine/`. Current UI prefabs are under `Assets/Prefab/UI/Machines_new/`; older UI prefabs also remain under `Assets/Prefab/UI/Machines/`, so confirm the `CanvasManager` registry before editing a visual.

## Runtime flow

1. Player trigger discovery finds a `MachineInstance` and requests interaction.
2. The server acquires the machine for one player and targets `StartMachineRpc` to that owner.
3. The owner-side `CanvasManager` instantiates the registered UI prefab and locks locomotion.
4. A machine-specific UI sends bounded results/input to its machine RPC.
5. The server validates/applies the resource or movement effect. Tutorial completion callbacks observe successful operations.
6. Closing or losing the machine releases the server lock and local movement/UI.

## Current prefab wiring

`MainShip - 3D.prefab` currently contains `CoolantMachine`, `FuelConverterMachine`, `FuelLevelUI`, `FuelSystemManager`, `MapNavigationMachine`, `OxygenMachineController`, `OxygenMachineInstance`, `OxygenSystemManager`, `PressureMachine`, `PressureSystemManager`, `PumpMachine`, `WaterSystemManager`, and `MachineManager`-related dependencies. Individual machine prefabs live under `Assets/Prefab/Ship/Machine/Minigame/`.

## Risks and known gaps

- `FuelConverterMachine` expects `FuelItem`, but no current prefab attachment was found.
- `OxygenMachineController` searches for `PumpHandleVisualizer`, but no current prefab attachment was found.
- Pressure has overlapping manager/global simulation ownership.
- Old minigame/resource implementations are listed in [Exclusions](../EXCLUSIONS.md). Do not mix them into the active resource path by name alone.
- RPC result validation varies by machine; treat all client-provided amounts/results as untrusted when extending a minigame.

## Update this page when

Change machine acquisition, UI registry/types, resource manager ownership, active machine/prefab composition, RPC validation, minigame completion outputs, or the legacy/current boundary.
