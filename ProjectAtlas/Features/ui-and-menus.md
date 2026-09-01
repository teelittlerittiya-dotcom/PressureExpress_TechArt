# UI and menus

Status: partial — migration wiring is intentionally explicit  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

UI covers the main menu, session/pause overlay, settings, cursor ownership, return-to-menu behavior, and the registry/lifecycle for machine minigames. The project currently contains both an older uGUI path and a newer UI Toolkit path; they are not yet fully switched over.

## Canonical files

| Path | Role |
|---|---|
| `Assets/Script/UI/Toolkit/MainMenuView.cs` | New UI Toolkit host/join/settings/quit view. |
| `Assets/Script/UI/Toolkit/SessionPanelView.cs` | New persistent in-session overlay. |
| `Assets/Script/UI/Toolkit/SettingsController.cs` | UI Toolkit settings panel controller. |
| `Assets/UI/MainMenu.uxml` | New main-menu tree. |
| `Assets/UI/SessionPanel.uxml` | New session overlay tree. |
| `Assets/UI/SettingsPanel.uxml` | Shared Toolkit settings tree. |
| `Assets/UI/PressureExpress.uss` | Shared Toolkit styling. |
| `Assets/Script/UI/Setting & Main Menu/MainUI.cs` | Current old uGUI main-menu session view. |
| `Assets/Script/UI/Setting & Main Menu/SettingsMenu.cs` | Old uGUI display/audio settings. |
| `Assets/Script/UI/Setting & Main Menu/SettingUI.cs` | In-scene pause/settings toggle. |
| `Assets/Script/UI/Setting & Main Menu/ReturnMenu.cs` | Leaves session or loads fallback menu. |
| `Assets/Script/UI/CanvasManager.cs` | Machine UI prefab registry and active-instance owner. |
| `Assets/Script/UI/MachineUIType.cs` | Machine UI identity enum. |
| `Assets/Script/UI/CursorVisibilityController.cs` | Reference-counted cursor/UI ownership. |

## Main-menu state today

`MainMenu.unity` contains the old `MainMenuCanva` active and new `UI_MainMenu` inactive. `MainMenuView` is a complete replacement implementation that calls `SessionService`, validates/normalizes `RoomCode`, supports local/Steam availability, and layers optional Feel feedback over USS transitions. Do not assume it is the live menu until the scene activation is deliberately changed and tested.

`QQuit.cs` has no current component GUID attachment, but old prefab UnityEvents retain `Quit` type metadata. Treat it as migration ambiguity, not proven dead code.

## Session-overlay state today

`SessionPanelView` is designed to be spawned once by `GameBootstrap`, persist across scenes, open on Escape only while `SessionState.InSession`, lock local movement without changing `Time.timeScale`, and expose copy/invite/settings/leave. However `Bootstrap.unity` currently leaves `GameBootstrap.sessionUIPrefab` null, so the new overlay is not live through its intended path.

The older `SessionPanel.cs` has no current attachment and is listed as suspected unused. `SettingUI`/`ReturnMenu` instances are still present in current gameplay content.

## Machine UI flow

1. `MachineInstance` accepts one player on the server and targets the accepted owner.
2. `CanvasManager` looks up the prefab for `MachineUIType`, instantiates it under `uiContainer`, owns one current machine UI, and locks player movement.
3. The minigame talks to its machine instance and reports tutorial completion where applicable.
4. Close destroys/releases the UI, machine lock, cursor ownership, and movement lock.

Current machine UI variants exist in both `Assets/Prefab/UI/Machines/` and `Assets/Prefab/UI/Machines_new/`; the serialized `CanvasManager.machineUIPrefabs` registry decides which is active.

## Shared contracts

- Menu/session UI calls `SessionService`; it must not create a second NetworkManager.
- Cursor visibility is reference-counted by owner. Balanced `OpenUI`/`CloseUI` calls prevent one overlay hiding another overlay's cursor.
- Multiplayer overlays do not pause world time.
- Machine UI identity values are serialized; do not reorder/renumber `MachineUIType` casually.
- UI movement locks must release on close, disable, destruction, leave, and error paths.

## Risks and unknowns

- The migration currently has an active-old/inactive-new split and an unwired new session prefab. Validate the live scene after any UI cleanup.
- Button UnityEvents can preserve type names even after a component is removed; raw text matches are evidence, not proof of a live script.
- `SettingUI` appears more than once in current `MainLevel`; confirm intended ownership before converting it to a singleton.

## Update this page when

Switch active menu generation, wire the session overlay, change UXML/USS contracts, session UI behavior, cursor/movement locks, machine UI registry/type values, or menu/session prefab composition.
