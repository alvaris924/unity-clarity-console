# ADR-0003: Zero third-party dependencies

- Status: accepted
- Date: 2026-09-15

## Context

UPM resolves a package's declared dependencies only from the Unity registry or from scoped registries the user has already configured. A package cannot declare a git URL dependency, and it cannot declare a scoped registry. A dependency on an OpenUPM-only package such as UniTask therefore fails to resolve for anyone installing from a git URL until they install that package by hand.

## Decision

The package declares no third-party dependencies. Only built-in modules and Unity-registry packages may appear in `package.json`. Optional integrations compile behind asmdef version defines, which activate only when the user's project already contains the package in question. Editor scheduling uses `EditorApplication.update` and UI Toolkit's scheduler; async libraries are not needed.

## Consequences

- One-step install from a git URL or OpenUPM on any supported version.
- Some conveniences are hand-rolled: a concurrent queue drained on the update tick, hand-written JSON through Unity's serializer, a small binary journal format.
- Contributors used to UniTask or R3 must not introduce them; the pull request checklist asks explicitly.

## Alternatives considered

- Depending on UniTask and documenting a manual pre-install: rejected, most users abandon at that step.
- Vendoring third-party source: rejected, it duplicates code the user's project may already contain and creates type collisions.
