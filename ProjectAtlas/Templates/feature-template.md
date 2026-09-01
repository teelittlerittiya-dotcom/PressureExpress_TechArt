# Feature name

Status: verified | partial | provisional  
Last verified: YYYY-MM-DD, commit and Unity version

## Responsibility

One paragraph describing what this system owns and explicitly does not own.

## Canonical files

| Path | Role |
|---|---|
| `Assets/...` | Primary responsibility |

## Runtime flow

1. Entry point.
2. State mutation and authority.
3. Downstream consumers.

## Contracts and state

- Public interfaces, events, RPCs, NetworkVariables, or shared types.
- Invariants and validation rules.

## Unity wiring and data

- Scenes, prefabs, ScriptableObjects, UI documents, layers/tags, and Build Settings assumptions.

## Dependencies

- Upstream systems this reads or calls.
- Downstream systems that consume it.

## Risks and unknowns

- Verified gaps, duplicate ownership, unwired paths, or runtime behavior not exercised.

## Update this page when

- Concrete architectural triggers for this feature.
