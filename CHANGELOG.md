# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Timeline strip: a thin bar chart above the list shows how many entries landed in each slice of the visible time span, stacked by severity, so a burst of errors stands out at a glance. Clicking a bar jumps the list to that moment and pauses auto-scroll. The strip follows every filter, so it charts what the list shows.
- Log file import: drop a `Player.log`, an Editor log or an Android `logcat` dump on the console, or pick one from the File menu, and it opens in its own read-only window. Unity logs keep their stack traces and call sites as clickable frames; logcat lines keep their level and turn their tag into a channel. The toolbar's Export menu is now File, holding the import action alongside saving and copying.
- Error Pause and clear-on options: a toolbar toggle pauses Play mode as soon as an error, exception or assertion is logged, and a dropdown next to Clear empties the console when entering Play mode, when a recompile starts or when a player build starts. All four are per-user preferences rather than project settings, since they are a personal habit.
- Export: the toolbar's Export menu writes the rows currently shown to a text, Markdown or JSON file, or copies them to the clipboard, and every export carries a header naming the Unity version, the package version, the session and the filters that were active. The row context menu gains "Copy for a bug report", which copies one entry with its stack and that same header.
- Watch rows: a message that starts with a watch key, `[watch:PlayerHP] 87` by default, takes over the row of the previous message with the same key instead of adding a new one, so a value logged every frame reads as one line that changes and carries an update count. Every update is still captured and journaled. The pattern is configurable in Project Settings and can be emptied to turn the behaviour off.
- Ignore rules: right-click a row to silence that message or its whole channel, and the status bar says how many entries are hidden. Rules live in Project Settings where they can be turned off or removed; entries are only hidden from the window, so capture and the journal keep them and removing a rule brings them back.
- A row context menu with copy actions: the message on its own, or the message with its stack trace.
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
