# Data assets and configuration

Status: partial — canonical families inventoried; every serialized value was not copied into Atlas  
Last verified: 2026-08-30

## Principle

Atlas documents which data asset owns a decision and where the assets live. It does not duplicate serialized tuning values; Unity assets are the source of truth for those values.

## Canonical asset families

| Family | Type/owner | Location | Used by |
|---|---|---|---|
| Cargo identity | `CargoItemData` | `Assets/Data/Cargo/` | `CargoController` |
| Cargo condition modules | `CargoModule` derivatives | Cargo subfolders | cargo runtime modules |
| Cargo polish | `CargoPolishProfile` + Feel prefabs | `Assets/Data/Cargo/Polish/` | `CargoPolishController` |
| Grip tuning | `GripConfiguration` | `Assets/Data/Holding/Default Grip Configuration.asset` | player hand/grab/solver |
| Route node content | `MapData` | `Assets/Script/Map System/MapNodeGenerator/Map Data/` | `MapNodeManager`, `MapGenerate` |
| Route difficulty | `MapDifficultySetting` assets | map-node data/configuration area | graph generation |
| Tile generation | TileWorld configuration | `Assets/Scenes/MainLevel/MainMapConfig.asset` | `MapGenerate` |
| Room types/resources | `RoomTypeSO` / current room assets | `Assets/Data/Resource/` and ship prefab assignments | room/ship simulation |
| Machine base/water | `MachineData`, `WaterLevelMachineData` | machine folders/prefabs | active pump/machine path |

## Cargo content notes

Current folders include Eggs, Nuke, Prototype, Test Variants, Polish, and `_unfinished`. Names such as prototype/test/unfinished are content maturity labels, not proof of unused scripts. Runtime selection code should use an intentional catalog rather than blindly including every `CargoItemData` found under the root.

The single canonical cargo prefab is `Assets/Prefab/Cargo/CargoController (new).prefab`; new cargo types should normally add data/module/polish assets, not fork this prefab.

## Map content notes

Current `MapData` assets include Start, Blank, Danger, Destination, Mystery, and Treasure. Each carries the map prefab/type and environmental water temperature/pressure used for the selected node. Graph probability/depth/count belongs in difficulty data, while layout/build layers belong in TileWorld configuration.

## Legacy data caution

The Oxygen/Power/Pressure/Temperature `MachineData` subclasses and the old resource assets form a suspected-unused cluster; see [Exclusions](../EXCLUSIONS.md). `MachineData.cs` and `WaterLevelMachineData` remain part of the routed current machine area. Do not delete assets because their script class is listed without first checking asset dependencies in Unity.

## Asset-change checklist

- Inspect dependencies and live prefab/scene assignments through Unity.
- Confirm server/client access to any ScriptableObject used in replicated initialization.
- Keep asset IDs/names stable when external catalogs or serialized references rely on them.
- Validate cargo collider/polish assets with the provided editor validators.
- Update the relevant feature page when an asset family moves or its ownership changes; do not record every tuning-number edit.

## Update this page when

Add/move/remove a canonical asset family, change the one-prefab/data-driven rule, change which asset owns a tuning decision, or retire/activate a legacy data cluster.
