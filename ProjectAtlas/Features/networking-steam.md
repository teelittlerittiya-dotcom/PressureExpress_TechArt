# Networking, Steam, and voice

Status: verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

This system owns host/join/leave lifecycle, NGO connection approval and scene loading, Steam lobby/relay integration, room-code behavior, player spawning, network diagnostics, voice bootstrap/occlusion, and Discord presence. It does not own player movement, cargo physics, or ship simulation.

## Canonical files

| Path | Role |
|---|---|
| `Assets/Script/Network/SessionService.cs` | Authoritative session façade and lifecycle state machine. |
| `Assets/Script/Network/SteamService.cs` | Steam initialization callbacks, lobby/invite/connect-string integration. |
| `Assets/Script/Network/RoomCode.cs` | Six-character normalization, validation, generation, and deterministic local port mapping. |
| `Assets/Script/Network/NetworkTypes.cs` | Session mode/state/result contracts and messages. |
| `Assets/Script/Network/PlayerSpawner.cs` | Server-side spawn-point assignment and player creation. |
| `Assets/Script/Network/NetworkDebugOverlay.cs` | F3 network/Steam/session diagnostics. |
| `Assets/Script/Network/VoiceChat/VivoxManager.cs` | Persistent Vivox lifecycle. |
| `Assets/Script/Network/VoiceChat/PlayerVoiceController.cs` | Player-owned voice registration/control and local speaking-light presentation. |
| `Assets/Script/Network/VoiceChat/VivoxAudioHandler.cs` | Vivox participant audio hookup. |
| `Assets/Script/Network/VoiceChat/VivoxOcclusionHandler.cs` | Per-player room/water voice filtering. |
| `Assets/Script/Network/Discord/DiscordManager.cs` | Discord SDK lifecycle. |
| `Assets/Script/Network/Discord/RichPresence.cs` | Presence payload/update helper. |

## Session flow

1. `GameBootstrap` chooses local loopback or Steam and creates the matching NGO `NetworkManager`.
2. `SessionService` subscribes to NGO/Steam callbacks and exposes asynchronous `HostAsync`, `JoinByCodeAsync`, and leave operations.
3. Local mode maps the normalized room code to `127.0.0.1` and a deterministic port in 7000–7999.
4. Steam hosting creates a lobby carrying `px_game=PressureExpress` and `px_code`, then uses Facepunch transport/relay. Lobby ID, invite, `+connect_lobby`, and room-code joins converge on the same connection path.
5. NGO connection approval validates code/capacity and accepts or rejects the client. On success the server loads `MainLevel` through NGO SceneManager.
6. `PlayerSpawner` creates each player at a configured spawn point. Leave tears down the lobby/network and returns to the menu path.

## Authority and contracts

- The server owns session acceptance, capacity, network scene changes, and player spawning.
- `SessionState`/`SessionResult`, `StateChanged`, and `StatusChanged` are the UI-facing contract.
- Room codes are uppercase, separator-free, exactly six characters after normalization.
- Steam identity included in a connection payload is self-reported; do not treat it as an authorization credential without server-side verification.
- Network gameplay rules are centralized in [Network authority](../CrossCutting/network-authority.md).

## Unity wiring

- Persistent service prefabs: `Assets/Prefab/Bootstrap/SessionService.prefab` and `SteamService.prefab`.
- Transport/NGO prefabs: `Assets/Prefab/Bootstrap/[Local] NetworkManager.prefab` and `[Steam] NetworkManager.prefab`.
- Player spawning prefab: `Assets/Prefab/Managers/[MANAGER] PlayerSpawn.prefab`.
- Player network prefab: `Assets/Prefab/Player/Player.prefab`.
- Live `Player.prefab` contains `PlayerVoiceController` and `VivoxOcclusionHandler`. `PlayerVoiceController.physicsHeadLure` references the nested production lure. Detected speech can locally change an optional 2D bulb-light radius from 0.4 to 1; the directly authored custom 3D Spot Light is intentionally untouched. The legacy direct `Light2D` field remains a fallback for older prefabs and is empty on the production player.
- `TestUI` is present in current `MainLevel` content; its test-like name is not evidence that it is unused.

## Dependencies

Requires the bootstrap-selected `NetworkManager`, Unity Netcode for GameObjects, Unity Transport for local play, Facepunch transport and Steamworks for Steam play, and optional Unity Services/Vivox/Discord managers. Player voice presentation also depends on the local `PhysicsHeadLure2D`/`HeadLureBulbVisual`; it does not replicate lure physics or optional 2D speaking-light radius. UI Toolkit and legacy menu views both call `SessionService`.

## Risks and unknowns

- Steam-down behavior intentionally disables multiplayer in player builds rather than silently falling back to unreachable loopback.
- Voice has multiple test/support scripts; validate the actual service/prefab before removing them.
- Approval payload trust and disconnect/reconnect edge cases deserve security/play-mode tests before release changes.

## Update this page when

Change session state/result APIs, transport selection, lobby metadata, room-code rules, connection approval, scene loading, spawn policy, voice lifecycle, or Discord integration.
