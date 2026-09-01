# Pressure Express agent rules

## Project Atlas is the code map

Before a repository-wide search, read `ProjectAtlas/INDEX.md`, then the feature pages named by its routing table. Use `ProjectAtlas/SYSTEMS.json` when selecting pages from changed file paths.

`ProjectAtlas/` describes the current implementation: ownership, runtime flow, wiring, authority, data, and known uncertainty. `Assets/Docs/` remains the home of plans, checklists, and worklogs. Do not turn Atlas pages into plans or copy plan status into them.

Source code and live Unity state are authoritative. If they disagree with Atlas, verify the implementation and update Atlas in the same change. Keep uncertainty explicit; never convert “suspected unused” into a deletion decision.

## Unity work

- Run `unity status --format json` before scene, prefab, or asset work.
- Prefer Unity CLI inspection and live Editor operations for scenes, prefabs, components, and assets.
- Do not hand-edit Unity serialized YAML (`.unity`, `.prefab`, `.asset`) when the live Editor can perform the operation.
- The expected project is `D:/Utiny Project/PressureExpress`. Stop if Unity CLI reports another project.

## Atlas maintenance contract

Update the relevant Atlas page when a change alters any of these:

- responsibility or ownership of a class/system;
- startup/runtime flow or dependencies between systems;
- a public contract, event, interface, network RPC, or replicated state;
- server/client authority or validation rules;
- scene, prefab, UI document, ScriptableObject, or Build Settings wiring;
- canonical file locations, replacements, or legacy status.

A private refactor needs no Atlas edit when the documented behavior and routing remain true. New first-party C# files must be assigned in `ProjectAtlas/SYSTEMS.json`. Suspected-unused files belong in `ProjectAtlas/EXCLUSIONS.md`; listing one never authorizes deletion.

Before handing off code changes, run:

```powershell
& ./ProjectAtlas/Tools/Validate-Atlas.ps1 -WriteCoverage
```

In the final task summary, include `Atlas impact: updated` or `Atlas impact: N/A — <reason>`.

## Worktree safety

Preserve unrelated user changes. Do not rewrite files in `Assets/Docs/` merely to keep Atlas synchronized; the two collections have different purposes.
