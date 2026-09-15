# ADR-0001: Capture logs through public Unity APIs only

- Status: accepted
- Date: 2026-09-15

## Context

The built-in Console reads a native log store that is only reachable through the internal `UnityEditor.LogEntries` class. Third-party consoles traditionally reflect into it because it offers pre-subscription history, the context object of each entry and shared Clear semantics. The cost is a dependency on undocumented internals that shift between Unity versions and break without warning.

## Decision

The default capture path uses public APIs only: `Application.logMessageReceivedThreaded` for every log, a wrapping `ILogHandler` to recover the context object, and `CompilationPipeline` events for compiler messages. A thread-static slot correlates the handler call with the callback for the same message, so no heuristics are needed. Entries are persisted to a journal under `Library/` so history survives domain reloads and Editor restarts without the native store.

## Consequences

- Works unchanged on every supported Unity version and needs no version-specific adapters.
- Logs emitted before the package subscribed are not visible; in practice that is the sliver of time before `[InitializeOnLoad]` runs.
- Clear in Clarity Console does not clear the built-in Console and vice versa.

## Alternatives considered

- Reflection over `LogEntries`: full parity with the built-in window, rejected as the default because of version fragility. May return later as an opt-in mirror in an isolated, version-guarded assembly.
- Patching the built-in window with Harmony: adds a native dependency and couples the package to Editor internals. Rejected.
