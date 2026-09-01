# Player

Status: verified  
Last verified: 2026-08-30, working tree based on `0196f196`, Unity 6000.3.10f1

## Responsibility

The player system owns owner-simulated 2.5D locomotion, movement state, facing/visual animation, swimming, ladders, drop-through behavior, machine discovery, spatial footsteps, local camera targeting, and the local-only articulated head-lure presentation. Cargo grip/hand mechanics are separated into [Holding and hands](holding-and-hands.md).

## Canonical files and assets

| Path | Role |
|---|---|
| `Assets/Script/Player/CharacterController2D.cs` | Current locomotion/network state controller despite the historic “2D” name. |
| `Assets/Script/Player/PlayerEyeballs.cs` | Eye look/presentation; two instances are on the player prefab. |
| `Assets/Prototype/HeadLurePhysics/Scripts/PhysicsHeadLure2D.cs` | Builds and updates the visual-only articulated 2D lure chain. |
| `Assets/Prototype/HeadLurePhysics/Scripts/HeadLureBulbVisual.cs` | Configures and mirrors only the detached bulb sprite; it resolves optional authored light children without overwriting their settings. |
| `Assets/Prefab/Player/Player.prefab` | Canonical network player prefab. |
| `Assets/Prototype/HeadLurePhysics/PhysicsHeadLure.prefab` | Reusable lure nested under the canonical player's `Anim-Body/Sprite-Bulb` socket. |
| `Assets/Prototype/HeadLurePhysics/HeadLureBulbVisual.prefab` | Runtime bulb visual instantiated at the final physics node. |
| `Assets/Prototype/HeadLurePhysics/HeadLure_AllIn1Pixelated.mat` | All In 1 Sprite Shader material shared by the rope and bulb with `PIXELATE_ON`. |
| `Assets/Prefab/Managers/[MANAGER] PlayerSpawn.prefab` | Server spawn coordinator. |

## Runtime flow

1. The server creates `Player.prefab`; ownership determines which client reads local input and simulates movement.
2. `CharacterController2D` drives a 3D `Rigidbody`/collider on the XY gameplay plane and writes owner movement state/position/facing NetworkVariables.
3. Each client locally initializes the nested `PhysicsHeadLure2D`: it anchors to the inherited visual-facing socket, creates an isolated `Rigidbody2D`/joint chain, and instantiates `HeadLureBulbVisual` at the final node. This is presentation only and is not replicated.
4. `PlayerVoiceController` may change an optional 2D speaking-light radius. The production bulb's custom 3D Spot Light is prefab-authored and is intentionally never changed by the voice script.
5. Other peers consume replicated state for remote visuals. Server-written interaction state communicates machine use.
6. Trigger discovery identifies nearby `MachineInstance` objects. Interaction acquisition and release are server-validated by the machine.
7. On server spawn the controller creates the networked `PlayerHand`; cargo interaction is then delegated to the hand/grab components.
8. Each `PlayerEyeballs` runs after `PlayerHand` presentation and derives its clamped pupil offset from that player's registered hand transform on every peer. It recenters while the hand is unresolved instead of reading the OS cursor or replicating a separate eye-intent value.
9. The controller chooses and emits footsteps through the spatial audio path; the old standalone `Footstep` component is excluded.

## Contracts and invariants

- Only the owning client reads movement input.
- Authoritative player/cargo physics is 3D, constrained to an XY presentation plane. The head lure is a deliberate visual-only 2D exception: its bodies and colliders must not influence gameplay movement, cargo, or authority.
- The lure contains no `NetworkObject`/`NetworkBehaviour`; peers may simulate different wiggle details while sharing the replicated player facing/position that drives its socket.
- `CharacterController2D.LockMovement`/`UnlockMovement` and the compatibility `canMove` state are shared by machine/session UI. Every lock path must release.
- Player interaction state is server-owned even though movement state is currently owner-written.
- The registered `PlayerHand` transform is the single gaze target for the pupils; eye presentation must not project `Input.mousePosition` independently from the virtual hand.
- The player prefab is a shared dependency of networking, camera, voice, holding, cargo, and machine UI.

## Current prefab wiring

Live prefab inspection found `CharacterController2D`, `CargoGrabController`, `CursorIntentProvider`, two `PlayerEyeballs`, `PlayerVoiceController`, and `VivoxOcclusionHandler` on `Player.prefab`. Its former authored `Anim-Bulb`/`Light 2D` hierarchy has been removed and replaced with a nested `PhysicsHeadLure.prefab`; `PlayerVoiceController.physicsHeadLure` points to that nested component. `HeadLureBulbVisual.prefab` contains separate `Bulb Sprite` and `Custom Spot Light` children. Facing changes use `SpriteRenderer.flipX`, so the bulb root and Spot Light never inherit a negative scale. The Spot Light's component and child-transform settings are authored directly in the prefab. `PlayerHand` remains a separate network prefab spawned by the server.

## Dependencies

Depends on NGO, Input System/legacy input compatibility, `UpdateManager`, `PlayerSpawner`, `MachineInstance`, room/water queries, Vivox/spatial audio, the camera system, URP 2D lighting types, and the All In 1 Sprite Shader asset. Holding consumes the player transform, cursor intent, and ownership.

## Risks and unknowns

- Owner-written transform/state is responsive but places validation responsibility on the server-side interaction and game rules.
- The class name suggests 2D while implementation uses 3D physics; preserve the actual physics contract.
- Head-lure wiggle is intentionally client-local and nondeterministic. Do not promote its generated `Rigidbody2D` nodes into network prefabs or gameplay collision without a separate authority design.
- Several older player controllers/helpers remain in [Exclusions](../EXCLUSIONS.md).

## Update this page when

Change movement authority, replicated player state, movement modes, interaction discovery, movement locking, player prefab composition, hand spawning, footsteps, or camera ownership.
