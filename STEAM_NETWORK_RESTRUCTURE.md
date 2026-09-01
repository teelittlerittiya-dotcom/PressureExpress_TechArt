# Steam network restructure

Branch: `steam-network-restructure` (off `new-poc`).

## Why the old code failed

Four independent blockers, all of which had to be fixed for Steam to work at all.

**1. Steam was being shut down process-wide, seconds after launch.**
`MainMenu.unity` contained a live `[Steam] NetworkManeger` prefab instance, and the
`AppicationManager` component on the `GameManager` GameObject held a reference to *that scene
instance*. `SpawnManagerObj()` then did `Instantiate(steamNetworkMenager)`, cloning an object that
was already alive. The clone's `SteamNetworkMenager.Awake` saw an existing `Instance` and called
`Destroy(gameObject)`; at end of frame the clone's `FacepunchTransport.OnDestroy` ran, and that
calls the **static** `SteamClient.Shutdown()`. From that moment `SteamClient.IsValid` was false
forever, so every host/join hit the `!SteamClient.IsValid` branch and started a **UnityTransport
host on 127.0.0.1**. That is the whole "same PC only, any code joins, nothing in the friends tab"
symptom, in builds as well as the Editor.

Because `Destroy` is deferred, `await steamNetworkMenager.Init()` still ran on the doomed clone,
subscribing Steam callbacks to a dying object and adding a **second `onClick` listener** to the same
scene buttons. `OnDestroy` then bailed at `if (Instance != this) return;` without unsubscribing.

**2. The host could never leave the menu on the Steam path.**
`LoadLobbyScene()` was nested inside `if (VivoxManager.Instance != null)`, and `VivoxManager` was
never instantiated in the real flow — it lives on `VoiceMenager.prefab`, which nothing spawned. So
`Instance` was always null: `WaitForVivoxAsync` burned its full 3s timeout on every Host click, and
the scene load never executed.

**3. Builds could not initialise Steam.**
`steam_appid.txt` existed only at the project root, not in `Build/`. With no partner appid the game
is launched by running the exe directly, and `SteamAPI_Init` requires that file beside the
executable.

**4. Nothing published a join target.**
`SteamFriends.SetRichPresence("connect", ...)` was never called, so the friends-list "Join Game" had
nothing to bind to — the `+connect_lobby` *receiver* existed but the *sender* never did.
`OnGameRichPresenceJoinRequested` was not subscribed, so Shift+Tab → Join Game did nothing while the
game was running. `SetGameServer(lobby.Owner.Id)` additionally advertised a dedicated server that
does not exist.

Two more that would have bitten next:

**5.** `MainLevel.unity` instantiates `Assets/Prefab/Manager/NetworkManager.prefab`, which carries a
real `NetworkManager` + `UnityTransport` + `TestUI`. Loading the game scene creates a **second**
NetworkManager.
**6.** `roomSize` was `0` on the `[Steam] NetworkManeger` prefab asset — the `4` existed only as a
scene override — so hosting from the asset would have called `CreateLobbyAsync(0)`.

## What replaced it

| File | Role |
| --- | --- |
| `Assets/Script/GameBootstrap.cs` | Sole owner of all persistent managers. Lives only in `Bootstrap.unity`. |
| `Assets/Script/Network/SteamService.cs` | Steam readiness, rich presence, invite overlay, all three join entry points. |
| `Assets/Script/Network/SessionService.cs` | Host / join / approve / leave, room codes, scene load. |
| `Assets/Script/Network/NetworkTypes.cs` | `NetworkMode`, `SteamState`, `SessionState`, typed `SessionResult`. |
| `Assets/Script/Network/RoomCode.cs` | Code alphabet, normalisation, loopback port mapping. |
| `Assets/Script/Network/NetworkDebugOverlay.cs` | F3 in-build diagnostics. |
| `Assets/Script/Framework/UnityServicesBootstrap.cs` | Single owner of UGS init + anonymous sign-in. |
| `Assets/Script/UI/.../SessionPanel.cs` | Pause-menu room code + Invite Friends. |
| `Assets/Script/Editor/BootstrapSetupWizard.cs` | Generates the prefabs, the Bootstrap scene and build settings. |
| `Assets/Script/Editor/SteamAppIdPostBuild.cs` | Writes `steam_appid.txt` beside every build. |

Deleted: `SteamNetworkMenager.cs`, `AppicationManager.cs`, and the duplicate
`Assets/Plugins/Facepunch.Steamworks.2.4.1/` (the transport package ships the same managed assembly
enabled for Editor **and** Standalone Win64, plus the `steam_api64.dll` the builds were already
using).

### Design rules now enforced

- **Mode is decided once.** `Application.isEditor ? LocalLoopback : Steam`, in `GameBootstrap`.
  Transport selection happens by instantiating one of two prefabs, each carrying exactly one
  transport. Nothing swaps `NetworkConfig.NetworkTransport` at runtime.
- **Steam never initialises in the Editor**, because the `[Local]` prefab has no
  `FacepunchTransport` at all. Disabling the component would not have been enough — `Awake` runs on
  disabled components.
- **The transport owns the SteamClient lifecycle.** `SteamService` calls no `Init`, no
  `RunCallbacks`, no `Shutdown`.
- **No silent fallback.** In a build, if Steam is unavailable the menu says so and Host/Join are
  disabled.
- **Await-driven.** `OnLobbyCreated` and `OnLobbyEntered` are not subscribed; every flow is a single
  linear `async` method. Lobby data is written while the lobby is invisible and published
  afterwards.
- **One way out.** `SessionService.LeaveSessionAsync` is the only teardown: rich presence → voice →
  lobby → NGO shutdown → wait for `!IsListening` → menu scene.
- **Voice never gates anything.** Vivox joins are fire-and-forget after the session is up.

## Manual steps (Unity Editor)

1. **Open the project and let it compile.** Expect errors only about *missing scripts* on the old
   prefab/scene objects, since `SteamNetworkMenager` and `AppicationManager` are gone.
2. **Run `Tools ▸ PressureExpress ▸ Create Bootstrap Setup`.** This creates:
   - `Assets/Prefab/Bootstrap/[Local] NetworkManager.prefab` (UnityTransport only)
   - `Assets/Prefab/Bootstrap/[Steam] NetworkManager.prefab` (FacepunchTransport only)
   - `SteamService` / `SessionService` / `NetworkDebugOverlay` prefabs
   - `Assets/Scenes/Bootstrap.unity`, added at build index 0, with `Lobby.unity` removed
3. **Clean `MainMenu.unity`:** delete the `[Steam] NetworkManeger` instance, the inactive
   `AppicationManager` GameObject, and the `AppicationManager` component on `GameManager`. The
   dormant `TestVoiceChat` object can go too (nothing is wired to it).
4. **Clean `MainLevel.unity`:** delete the `NetworkManager` prefab instance (blocker 5 above).
5. **`MainUI` needs nothing** — its six original fields are unchanged, so the references already in
   `MainMenuCanva.prefab` reconnect. Optionally assign `statusText`, `localModeBanner`,
   `settingsPanel` and `discordUrl`.
6. **`SettingsMenu` needs nothing** — its three original fields reconnect. Optionally assign the
   voice device dropdowns.
7. **Add `SessionPanel`** to your in-game pause canvas in `MainLevel` and assign whichever of
   `roomCodeText`, `copyButton`, `inviteButton`, `resumeButton`, `leaveButton`, `feedbackText` you
   have. Leave `GameBootstrap`'s *Session UI Prefab* empty when doing it this way.
8. **Check the `[Steam] NetworkManager` prefab** has `DefaultNetworkPrefabs` in
   `NetworkConfig.Prefabs.NetworkPrefabsLists` (the wizard copies it from the old prefab).
9. Once you're satisfied, delete the old `Assets/Prefab/AppicationManager/[Steam] NetworkManeger.prefab`.

## UI (hand-made uGUI)

The menu, settings and in-game session panel use the existing hand-made uGUI prefabs. A UI Toolkit
version also exists under `Assets/UI` + `Assets/Script/UI/Toolkit` but is **not** used by the flow;
it compiles and is safe to delete (see the end of this section).

| File | Role |
| --- | --- |
| `Assets/Script/UI/Setting & Main Menu/MainUI.cs` | Binds `MainMenuCanva.prefab` to `SessionService`. |
| `Assets/Script/UI/Setting & Main Menu/SettingsMenu.cs` | Volume, fullscreen, resolution, optional voice devices. |
| `Assets/Script/UI/Setting & Main Menu/SessionPanel.cs` | In-game: room code, copy, Invite Friends, resume, leave. |
| `Assets/Script/Framework/DisplaySettings.cs` | UI-agnostic settings apply/persist, used by both UI systems. |

**Three bugs fixed in the existing UI.**

1. `SettingsMenu` read PlayerPrefs into its widgets on `Start` but never applied them, so a saved
   volume / fullscreen / resolution was ignored on every launch. `GameBootstrap` now calls
   `DisplaySettings.Apply()` before any UI exists.
2. All 13 `OnClick`/`OnValueChanged` lists in `MainMenuCanva.prefab` and the settings widgets in
   `MainMenu.unity` were **empty**, and neither script added listeners — so no menu button and no
   settings control was connected to anything. Every listener is now added in code.
3. `VivoxAudioHandler.InitializeDeviceUI()` dereferenced its dropdowns with no null check, inside
   the try block that sets `VivoxManager.IsInitialized` — so with those dropdowns unassigned, voice
   chat silently never initialised. Null-guarded, and it now exposes a plain device API.

**Removing the unused UI Toolkit version**, if you want it gone: delete `Assets/UI/` and
`Assets/Script/UI/Toolkit/`, then delete `Assets/Script/Editor/UIToolkitSetupWizard.cs`. Nothing
else references them.

## Testing

**Editor (LocalLoopback).** The project already has `com.unity.multiplayer.playmode` — use
Multiplayer Play Mode virtual players to get two instances. Host in one, read the code off the F3
overlay, type it in the other. A *wrong* code now genuinely fails, because the code maps to a
distinct port and the approval callback also checks it.

**Build (Steam), two PCs, two accounts.**
1. Build, confirm `steam_appid.txt` appeared next to the exe.
2. Launch both with Steam running. F3 on each: `Steam: Ready`, `IsValid: True`, a real SteamId.
   If you ever see the red "SteamClient was valid and then became invalid" line, something is
   calling `SteamClient.Shutdown()` again.
3. Host on A. F3 should show a lobby id, member count 1/4, and a non-empty `connect` string.
4. Join on B by code.
5. Pause menu on A → Invite Friends → accept on B.
6. Shift+Tab on B → A's name → Join Game, with B's game already running.

## Known limitations

- **The SteamId in the approval payload is self-reported.** `FacepunchTransport` does not surface
  the peer identity to NGO, so checking it against lobby membership stops someone who merely
  enumerated the public lobby list; it is not proof of identity.
- **A small code-collision race remains** between the availability check and publishing. A client
  that lands on the wrong host is rejected by the approval code check.
- **Public lobbies are required** for room codes to work — `FriendsOnly` lobbies are invisible to
  non-friends in `LobbyList`. On appid 480 that means the lobby is visible in the shared global
  Spacewar pool, which is why every query filters *and re-validates* on the `px_game` key.
- **Shift+Tab "Join Game" on appid 480 is unverified.** The `connect` rich-presence key is the
  documented mechanism and `SetGameServer` was working against it, but Spacewar is a shared sandbox
  and this can only be confirmed on the two-PC setup.
- **`BootstrapSetupWizard` has never been executed** — it was written without a Unity compiler
  available. Read its log output rather than assuming it did the right thing.
