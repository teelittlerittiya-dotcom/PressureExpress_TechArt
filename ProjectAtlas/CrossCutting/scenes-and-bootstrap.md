# Scenes, bootstrap, and prefab composition

Status: verified through Unity CLI, Build Settings, AssetDatabase dependencies, and live component inspection  
Last verified: 2026-08-30, Unity 6000.3.10f1, live Editor PID 9288

## Enabled Build Settings scenes

| Order | Scene | Purpose |
|---:|---|---|
| 0 | `Assets/Scenes/Bootstrap.unity` | Persistent composition root. |
| 1 | `Assets/Scenes/MainMenu.unity` | Host/join/settings/menu. |
| 2 | `Assets/Scenes/MainLevel.unity` | Main multiplayer game. |
| 3 | `Assets/Feel/MMTools/Core/MMSceneLoading/LoadingScreens/MMAdditiveLoadingScreenBigText.unity` | Loading presentation. |
| 4 | `Assets/Scenes/Development/TestSecenes.unity` | Enabled development/test content. |
| 5 | `Assets/Scenes/Development/Tutorial.unity` | Tutorial. |

`Assets/Scenes/Development/Sonar.unity` is present but disabled. `Lobby.unity`, `Minigame.unity`, `Test-MapGen.unity`, `Test-Minigame.unity`, `Test-NewMachine.unity`, and `Test-RougelikeInventory.unity` are not in Build Settings.

## Startup sequence

1. Start in `Bootstrap.unity`.
2. `GameBootstrap` selects and instantiates local or Steam `NetworkManager` plus persistent services.
3. Bootstrap loads `MainMenu`.
4. A successful `SessionService` host/join uses NGO SceneManager to load `MainLevel` for the session.
5. `PlayerSpawner` creates the network player. Scene/prefab managers supply ship, map, UI, audio, and simulation.
6. Session teardown returns to menu and destroys network/session state while persistent application services remain as designed.

## Key prefab composition

| Prefab | Verified first-party components |
|---|---|
| `Assets/Prefab/Player/Player.prefab` | `CharacterController2D`, `CargoGrabController`, `CursorIntentProvider`, two `PlayerEyeballs`, `PlayerVoiceController`, `VivoxOcclusionHandler`, plus nested local-only `PhysicsHeadLure.prefab` under `Anim-Body/Sprite-Bulb`. |
| `Assets/Prefab/Player/PlayerHand.prefab` | `PlayerHand`. |
| `Assets/Prefab/Cargo/CargoController (new).prefab` | `CargoController`, collider builder, hold solver, proximity sensor, polish controller, particle manager. |
| `Assets/Prefab/Ship/MainShip - 3D.prefab` | Submarine/room simulation, active machine family, sonar, SFX, room camera/water visualizers. |
| `Assets/Prefab/Managers/[MANAGER] Map Node.prefab` | `MapNodeManager`. |
| `Assets/Prefab/Managers/[MANAGER] MapGen.prefab` | `MapGenerate`, `MapTestScript`. |
| `Assets/Prefab/Managers/[MANAGER] MapMoveController.prefab` | `MapMoveController`. |
| `Assets/Prefab/Managers/[MANAGER] NavigationGameManager.prefab` | `CanvasManager`, `NavigationGameManager`. |
| `Assets/Prefab/Managers/[MANAGER] MachineManager.prefab` | `MachineManager`. |
| `Assets/Prefab/Managers/[MANAGER] PlayerSpawn.prefab` | `PlayerSpawner`. |

The local and Steam NetworkManager prefabs primarily contain NGO/transport components; first-party session logic lives in separate persistent service prefabs.

## Current `MainLevel` evidence

Live inspection found the expected ship/machine/map/player-support systems, four cargo instances, `CargoDebugMode`, `MapNetworkMovement`, `DrawController`, `MusicManager`, camera/effects, `ReturnMenu`, two `SettingUI` instances, and `TestUI`. Test/prototype naming alone is therefore not enough to classify a component unused.

## Scene and serialization cautions

- `Assets/Scenes/Boostrap.unity` is a misspelled second scene; Build Settings use `Bootstrap.unity`.
- `Assets/Scenes/Minigame.unity` contains unresolved merge-conflict markers and cannot be trusted as valid Unity YAML. It is not a build scene.
- `MainMenu.unity` currently has old `MainMenuCanva` active and new `UI_MainMenu` inactive.
- `Bootstrap.unity` currently has a null `GameBootstrap.sessionUIPrefab`.
- Do not hand-edit `.unity`, `.prefab`, or `.asset` YAML. Query/change through the live Unity Editor/CLI and save there.

## Verification recipe

Run `unity status --format json`, confirm the project path, inspect Build Settings, then query the relevant live scene/prefab hierarchy/components. A raw GUID search is useful supporting evidence but does not replace Unity dependency/component inspection.

## Update this page when

Change Build Settings, startup/session scene flow, canonical prefab composition, persistent-vs-scene ownership, active UI generation, or any caution above.
