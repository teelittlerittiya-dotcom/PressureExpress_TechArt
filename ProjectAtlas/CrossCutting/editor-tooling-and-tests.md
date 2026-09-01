# Editor tooling, tests, and runtime prototypes

Status: verified inventory  
Last verified: 2026-08-30

## Project-owned editor tools

| Path | Purpose |
|---|---|
| `Assets/Script/Editor/BootstrapSetupWizard.cs` | Creates/repairs bootstrap/network service setup. |
| `Assets/Script/Editor/UIToolkitSetupWizard.cs` | Creates/repairs UI Toolkit assets/prefab wiring. |
| `Assets/Script/Editor/TutorialSceneSetup.cs` | Tutorial scene setup assistance. |
| `Assets/Script/Editor/PlayModeSceneSelector.cs` | Selects startup scene behavior for Editor play mode. |
| `Assets/Script/Editor/SteamAppIdPostBuild.cs` | Post-build Steam app ID handling. |
| `Assets/Editor/CargoPrototypeValidator.cs` | Cargo prefab/data prototype validation. |
| `Assets/Editor/CargoPolishProfileValidator.cs` | Cargo polish asset validation. |
| `Assets/Editor/CargoPolishMigrationTool.cs` | Cargo polish migration helper. |
| `Assets/Editor/Physics2DTo3DConverter.cs` | Migration helper for current 3D physics convention. |
| `Assets/Editor/WeightedHoldingValidator.cs` | Holding/prefab/configuration validation. |

Setup wizards are repeatable editor automation, not runtime ownership. Before running one on an established scene/prefab, inspect what it creates or overwrites and preserve user wiring.

## First-party tests

| Path | Scope |
|---|---|
| `Assets/Tests/EditMode/CargoPrototypeEditModeTests.cs` | Cargo data/collider/module edit-mode behavior. |
| `Assets/Tests/PlayMode/CargoPrototypePlayModeTests.cs` | Cargo runtime play-mode behavior. |
| `Assets/Tests/EditMode/WeightedHoldingEditModeTests.cs` | Grip math/state/configuration. |
| `Assets/Tests/PlayMode/WeightedHoldingPlayModeTests.cs` | Runtime/server-style holding behavior. |

Run the affected suites after cargo, generated collider, grip, player hand, or rigidbody changes. Networking/session/ship/map/tutorial currently have less obvious first-party automated coverage; compensate with focused Unity play-mode verification.

## Current helpers/prototypes in runtime content

- `Assets/Script/Map System/LevelGenarator/MapTestScript.cs` is attached to the current MapGen prefab.
- `Assets/Script/Network/TestUI.cs` appears in current `MainLevel`.
- `Assets/Script/Misc/DrawingSystem/DrawController.cs` appears in current `MainLevel` and provides mouse-driven LineRenderer drawing.
- `Assets/Script/Cargo System/CargoDebugMode.cs` appears in current content.

The head-lure development harness remains under `Assets/Prototype/HeadLurePhysics/` with `PrototypePlayerDriver3D`, a stripped prototype player, and `Assets/Scenes/Development/HeadLurePhysicsPrototype.unity`. The reusable runtime pieces are now production player dependencies and are routed to the player feature: `PhysicsHeadLure2D` builds the local articulated chain, instantiates `HeadLureBulbVisual.prefab`, mirrors from the inherited head-socket Y rotation, and uses a dedicated All In 1 pixel material. The canonical `Player.prefab` nests `PhysicsHeadLure.prefab` under `Anim-Body/Sprite-Bulb`; the old `Anim-Bulb` hierarchy is gone. The harness remains useful for testing the visual simulation without starting NGO, while authoritative player motion continues to use constrained 3D physics.

These names may indicate development intent, but current attachment means they are not safe unused-code conclusions. Decide whether they ship through a separate feature/build policy, not filename guessing.

## Third-party boundary

The repository contains large imported trees such as Feel/MMFeedbacks, TileWorldCreator, UniTask, TextMesh Pro examples, 2D Water, shaders, and the Discord SDK. `SYSTEMS.json` intentionally covers first-party roots only: `Assets/Script`, `Assets/Editor`, `Assets/Tests`, and `Assets/Prototype`. Package/vendor internals should be read only when a task crosses that boundary.

## Atlas tooling

`ProjectAtlas/Tools/Validate-Atlas.ps1` checks first-party C# ownership/exclusion routing and Atlas links/pages. Run it with `-WriteCoverage` after architectural changes. `Generated/coverage.md` is derived output and should not be hand-maintained.

## Update this page when

Add/remove first-party tools/tests, change their feature ownership, attach/detach a prototype from current content, change third-party boundaries, or change Atlas validation behavior.
