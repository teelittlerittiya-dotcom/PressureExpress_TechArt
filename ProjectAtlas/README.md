# Project Atlas

Project Atlas is the AI-readable map of the current Pressure Express implementation. It answers: where a feature lives, which objects start it, what owns its state, what it calls, which assets wire it, and what is uncertain.

It is intentionally separate from `Assets/Docs/`:

| Collection | Purpose | Time orientation |
|---|---|---|
| `ProjectAtlas/` | Current architecture and verified wiring | What exists now |
| `Assets/Docs/` | Feature plans, checklists, decisions, and worklogs | What is intended or was done |

## Reading order

1. Read `INDEX.md`.
2. Open only the feature pages relevant to the task.
3. Read a cross-cutting page when the change touches scenes, networking, physics, data, or editor tooling.
4. Consult `EXCLUSIONS.md` before treating an old-looking script as active or deleting it.

Each page separates verified facts from cautions. “Partial” means the code was inspected but every serialized value or runtime path was not exhaustively exercised. Source and live Unity state always win when the Atlas is stale.

## Maintenance

`SYSTEMS.json` is the machine-readable file-to-system router. `Tools/Validate-Atlas.ps1` verifies that every first-party C# file is routed or deliberately excluded, checks page targets, and can regenerate `Generated/coverage.md`.

Atlas edits should be compact and architectural. Do not paste entire class APIs or maintain line numbers that become stale quickly. Record paths, responsibilities, key contracts, data flow, authority, asset wiring, and important failure modes.
