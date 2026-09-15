# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Frame folding: runs of infrastructure frames in the detail pane collapse into one row that names what it hid, for example "3 frames hidden (UnityEngine, Cysharp)", and unfold on click. The engine, the Editor, the runtime and this package fold by default; projects add their own wrappers as type-name prefixes in Project Settings. Opening an entry now lands on the first frame that is not folded, so a project's logging wrapper is skipped as well.
- Source preview: selecting a stack frame shows the lines around it with the frame's own line highlighted, read straight from disk and re-read when the file changes. Clicking a frame now selects and previews it; double-clicking it, or the preview, opens the file in the code editor. The number of lines is configurable in Project Settings.
- Query language in the search field: terms are joined by AND, `"quoted phrases"` match exactly, `-term` excludes, `/regex/i` matches a pattern, `sev:error,warn` and `tag:PlayFab*` filter, `in:stack` also searches stack traces, and `A OR B` matches either. A malformed query explains itself in the status bar instead of failing silently.
- Channels: messages that start with a tag such as `[PlayFabCBSManager]` are grouped into channels, shown as a row of chips with live counts under the toolbar. Clicking chips narrows the list to those channels, and the pattern is configurable in Project Settings under Clarity Console.
- Persistence: entries are journaled to `Library/ClarityConsole/` and restored after every domain reload and Editor restart, so the window no longer empties on recompile. The journal is segmented with a 32 MB budget, tolerates a torn tail after a crash, and is reset by Clear. Markers now read "Editor started" or "Domain reloaded"; context objects from a previous Editor session are dropped because their ids no longer resolve.
- Stack frames: the detail pane lists every frame of the selected entry, frames with a source location are links that open the file at that line in the configured code editor, double-clicking a row opens the first user frame, and selecting a row pings the entry's context object in the Hierarchy or Project window.
- Console window under `Window > Clarity Console`: a virtualized multi-column list bound to the capture store, severity toggles with live counts, case-insensitive search, exact-message collapse with counts, a detail pane with the message and raw stack trace, auto-scroll that pauses while you read older entries, and a status bar.
- Log capture: a wrapping log handler plus the threaded callback record every Editor log with its context object, thread and frame into `LogStore`, drained on the Editor update tick under a time budget, with Play mode and domain-load markers and a persisted Play session counter.
- Package scaffold: `package.json`, the six assembly definitions, `RingBuffer<T>` in `ClarityConsole.Core` with EditMode tests, the `DevProject~` development project, the CI test matrix and the tag-driven release workflow.
- Repository workflow: contribution guide, `AGENTS.md` rules for AI assistants, local git hooks, pull request and issue templates, Conventional Commit title check, Dependabot for GitHub Actions.

[Unreleased]: https://github.com/alvaris924/unity-clarity-console/commits/main

### Fixed

- The journal no longer keeps old entries when Clear cannot delete its file because something else holds it open; the file is emptied instead, and a journal that resumes an existing segment now reports its true size.
