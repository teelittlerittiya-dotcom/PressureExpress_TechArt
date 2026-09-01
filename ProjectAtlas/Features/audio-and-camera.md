# Audio and camera

Status: partial — core code and live components verified  
Last verified: 2026-08-30, commit `bd86b016`, Unity 6000.3.10f1

## Responsibility

Audio provides music/SFX volume, room-and-door spatial attenuation, underwater filtering, voice filtering, and reusable SFX sources. Camera follows the local player, supports ship/character zoom modes and tutorial external control, applies room overrides, and renders underwater effects.

## Canonical files

| Path | Role |
|---|---|
| `Assets/Script/Audio/SpatialAudioManager.cs` | Distance attenuation, room graph, closed-door occlusion, underwater low-pass, one-shots. |
| `Assets/Script/Audio/SFXSource.cs` | Registers an AudioSource and accepts volume/filter updates. |
| `Assets/Script/Audio/MusicManager.cs` | Persistent BGM tracks, crossfade, SFX/music volume controls. |
| `Assets/Script/Audio/VolumeSetting.cs` | Legacy slider bridge to volume controls. |
| `Assets/Script/Audio/UnderwaterVoiceAudioFilter.cs` | Water-dependent low-pass/pitch behavior for voice. |
| `Assets/Script/Camera/PlayerCameraController.cs` | Local-player follow, movement yaw, room transition, custom late update. |
| `Assets/Script/Camera/MainCamController.cs` | Character/ship zoom and external-control switch. |
| `Assets/Script/Camera/RoomCameraOverride.cs` | Per-room camera pitch/behavior override registry. |
| `Assets/Script/Camera/UnderwaterCameraEffect.cs` | Visual tint and underwater loop/filter based on water depth. |

## Spatial audio flow

1. `SFXSource` registers with `SpatialAudioManager`.
2. The manager lazily finds the local-owned network player and builds a room/door graph from `SubmarineManager` plus `DoorConnection`.
3. On a throttled custom update it applies distance rolloff, finds source/listener rooms, counts closed doors along the best path, and attenuates/low-passes accordingly.
4. Room water-surface queries add underwater low-pass behavior. Fire-and-forget one-shots create a temporary registered source.
5. Player footsteps now originate from `CharacterController2D`; the standalone `Footstep.cs` is suspected unused.

## Camera flow

1. The local player becomes the follow target of `PlayerCameraController`.
2. Camera follow runs through `ILateUpdateable`, uses the configured offset/smoothing, and can vary yaw with movement.
3. `RoomCameraOverride` changes room-specific presentation when the target crosses rooms.
4. `MainCamController` switches character/ship zoom presets. Tutorial preview temporarily takes external control.
5. `UnderwaterCameraEffect` queries room water surface to blend visual color and audio filtering by depth.

## Contracts and invariants

- Spatial audio uses the ship room graph; door endpoint/open-state errors affect audibility.
- `SFXSource.BaseVolume` is the unattenuated source level. Avoid competing scripts writing final AudioSource volume each frame.
- Local camera/audio listener logic must resolve the owned player, not an arbitrary player object.
- Water visualization queries are gameplay-adjacent shared spatial data, not merely visuals.
- External camera control must always be released.

## Current wiring

`MusicManager.prefab` is under `Assets/Prefab/Managers/`. `MainShip - 3D.prefab` has an `SFXSource` and ten `RoomCameraOverride`/water visualizer sets. Current `MainLevel` includes the camera controllers/effects and music manager.

## Risks and unknowns

- Full mixer/AudioSource serialized values and every clip assignment were not exhaustively audited.
- `SpatialAudioManager` requires at least one door to mark its room graph ready; validate open-plan/no-door layouts.
- Vivox has a separate occlusion/filter path in the networking feature; do not accidentally process voice twice.

## Update this page when

Change audio manager/source contracts, room/door attenuation, water filtering, music/volume ownership, footsteps, camera target/modes, room overrides, tutorial control, or camera/audio prefab wiring.
