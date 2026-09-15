# ADR-0002: Unity 2022.3 LTS is the floor

- Status: accepted
- Date: 2026-09-15

## Context

The package must be installable by as many projects as possible, including contract clients still on the 2022.3 LTS line, while day-to-day development happens on Unity 6000.2 and will move to 6000.6.

## Decision

`package.json` declares `"unity": "2022.3"`. Anything newer is version-guarded with `#if UNITY_6000_0_OR_NEWER` and similar, and the CI matrix grows to cover every claimed version. `MultiColumnListView`, list virtualization and `HideInCallstack` all exist in 2022.3, which is what makes the floor cheap.

## Consequences

- UI Toolkit element trees are built in C# rather than registered as UXML custom elements, so 2022.3 and 6000.x share one code path.
- One object-identity helper wraps the integer instance id versus `EntityId` difference; nothing else touches ids.
- Until the CI license secrets exist, 2022.3 support is claimed but only verified on the developer's 6000.x editors. The floor is revisited if that stays true for long.

## Alternatives considered

- 6000.0 as the floor: simpler code, smaller matrix, but excludes the still-large 2022.3 installed base. Kept as the fallback if maintaining the floor proves costly.
