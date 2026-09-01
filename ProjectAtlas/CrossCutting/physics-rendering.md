# Physics and rendering conventions

Status: verified for player, cargo, holding, ship, map collision, and camera integration  
Last verified: 2026-08-30

## World model

Pressure Express presents 2D-style sprites and movement but the current gameplay physics path is 3D. Player and cargo use `Rigidbody`/3D colliders constrained to an XY gameplay plane. The production player's articulated head lure is a local presentation-only 2D simulation and does not participate in gameplay authority. Exterior map content moves relative to the ship/interior frame.

## Core conventions

- Do not add `Rigidbody2D`/`Collider2D` to the current player-cargo-holding gameplay path. `PhysicsHeadLure2D` is the explicit visual-only exception: it dynamically owns an isolated chain beneath the sprite-facing socket and cannot drive the player's 3D body.
- Constrain gameplay motion/intent to XY; Z is primarily layering/depth presentation unless a system explicitly owns it.
- `CargoColliderBuilder` converts sprite physics outlines into convex 3D compound colliders. Sprite import geometry therefore affects impact and grip contact.
- `GripContactUtility` validates actual 3D contact/penetration; proximity/hover alone is not authority.
- `CargoHoldSolver` applies forces at the grip point on the server.
- `SpriteRenderOrderPolicy` standardizes cargo/2.5D sorting; avoid ad hoc sorting values that fight it.
- Exterior `MapNetworkMovement` moves the map frame and detects obstacle overlap; `SubmarineCollision` converts collisions to ship damage/leaks.

## Spatial frames

| Frame | Typical contents | Important consumers |
|---|---|---|
| Ship/interior | Player, cargo, rooms, machines, doors | holding, room simulation, audio, camera |
| Moving exterior map | Generated terrain, obstacles, exits, waypoints | navigation, collision, sonar, tutorial spawns |
| UI/camera | Screen/radar/node/minigame presentation | local client only |

When spawning a map obstacle/exit, parent/place it in the moving exterior frame. When testing room/cargo/audio queries, use the interior world positions and `RoomMarker.ContainsPoint`/water surface.

## Rendering and camera

Sprite sorting and Z placement should preserve physical XY contact. The camera follows the owned player, can apply room overrides, and can be temporarily externally controlled by the tutorial. Underwater visuals/audio depend on `RoomWaterVisualizer` spatial queries.

The production Universal Renderer uses Forward+ lighting. The large tiled floors, walls, shelves, pipes, and structural decorations in `Assets/Prefab/Ship/MainShip - 3D.prefab` use `Assets/Shaders/Sprite-3D-Lit-Clustered.mat` and the `PressureExpress/Sprite 3D Lit` shader. That shader samples URP 3D point/spot lights from each fragment's world position, so lights that touch an edge of a large `SpriteRenderer` are not discarded by a small per-renderer light list. It uses the renderer's sprite texture/color, flat two-sided geometry normals with low directional influence, and no shared authored normal map; do not replace it with generic URP `Lit` or the 2D `Sprite-Lit-Default` shader unless the rendering and light topology are changed together. Highlight compression and configurable light steps keep overlapping punctual lights compatible with the project's posterized/bloom-heavy presentation.

`PhysicsHeadLure.prefab` reads the player's visual Y rotation so its authored curve and detached bulb sprite mirror when facing changes. Its rope and bulb use `HeadLure_AllIn1Pixelated.mat` (`AllIn1SpriteShader/AllIn1SpriteShader`, `PIXELATE_ON`, `_PixelateSize = 6`); rope/bulb colors remain renderer/prefab settings. `HeadLureBulbVisual.prefab` keeps `Bulb Sprite` and `Custom Spot Light` on separate child objects. Sprite mirroring uses `SpriteRenderer.flipX`; the bulb root and light keep positive scales. The 3D Spot Light's type, transform, color, intensity, range, cone, enabled state, and shadows are authored directly on the prefab and are never overwritten by `HeadLureBulbVisual` or `PlayerVoiceController`.

## Change checklist

- Re-run cargo and weighted-holding edit/play mode tests after collider, rigidbody, timestep, grip force, or sprite physics-shape changes.
- Test host and remote client; authoritative physics must not be applied twice.
- Verify map collision layers and sonar ray/overlap masks after layer changes.
- Verify camera/underwater queries after room bounds or water visualizer changes.
- Verify the Forward+ shader variant compiles and inspect light falloff at both the center and edges of the large tiled ship renderers after changing the URP renderer, ship sprite material, punctual-light ranges, or bloom/exposure settings.
- Verify lure joint gaps, right/left sprite mirroring, sprite visibility, positive bulb/light scales, and the authored Spot Light state after head socket, facing, material, or lure-prefab changes.
- Use the live Unity Editor for collider, layer, prefab, and scene edits.

## Update this page when

Change physics dimensionality, plane constraints, generated colliders, force application, spatial frames, map collision layers, sorting policy, room bounds/water queries, camera conventions, renderer lighting mode, or canonical ship sprite material/shader wiring.
