# Clarity Console documentation

Clarity Console is an open-source, dependency-free Console window replacement for the Unity Editor, built on UI Toolkit and shipped as the UPM package `com.alvaris.clarity-console`.

## Layout

| Path | Purpose |
|---|---|
| `Editor/Core` | Pure C# core: entry model, ring buffer, query parser, stack-trace parser, journal. No engine references. |
| `Editor/Capture` | Log handler wrapper, threaded callback, drain loop, compiler messages, session markers. |
| `Editor/UI` | The Editor window, view model and UI Toolkit elements. |
| `Editor/Settings` | Project settings, user preferences, settings pages. |
| `Editor/Extensions` | The public extension API. The only assembly with a compatibility promise. |
| `Tests/Editor` | EditMode tests and fixtures. |
| `DevProject~` | Development Unity project. References the package with `file:../..`. Invisible to package consumers. |

## Decision records

- [ADR-0001: Capture logs through public Unity APIs only](adr/0001-log-source.md)
- [ADR-0002: Unity 2022.3 LTS is the floor](adr/0002-unity-floor.md)
- [ADR-0003: Zero third-party dependencies](adr/0003-dependency-policy.md)
- [ADR-0004: GitHub flow with tags, no development branch](adr/0004-branching-model.md)

## Feature ideas

Candidates beyond the v1.0.0 scope, ranked by how much they help day-to-day game debugging, live in [feature-ideas.md](feature-ideas.md). Add to that table rather than opening a design discussion in an issue first.

## The timeline

The strip above the list charts the rows currently shown across the time they span, one bar per slice, stacked as logs, warnings and errors from the bottom up. It follows every filter, so narrowing to a channel or a query redraws it for those rows alone. Click a bar to jump the list to that moment; clicking an empty slice lands on the nearest earlier one with entries. Hover for the exact span.

## Searching

The search field takes a small query language. Terms are joined by an implicit AND.

| Syntax | Meaning |
|---|---|
| `word` | Case-insensitive substring of the message |
| `"two words"` | The phrase, still case-insensitive |
| `-term` | Excludes; works with any term type |
| `/regex/i` | .NET regular expression; flags `i` and `m` |
| `sev:error,warn` | Severity list: log, warn, error, exception, assert |
| `tag:PlayFab*` | Channel, with `*` and `?` wildcards |
| `in:stack` | Text and regex terms also search stack traces |
| `A OR B` | Either side; binds tighter than the implicit AND |

Session markers always show, whatever the query, so the stream keeps its shape. A query that cannot be parsed matches nothing and says why in the status bar.

## Settings

Project Settings > Clarity Console holds the settings a team shares through version control. Frame folding lives there and is off by default, so every frame shows as it does in the stock console. Turn on folding of engine frames (the engine, the Editor, the runtime and Unity's own `Unity.*` packages), folding of frames compiled from installed packages under `Library/PackageCache` (your embedded and local packages are not affected), or add type-name prefixes of your own to fold, one per line, such as a logging wrapper or an async library, and folded runs collapse into one row that expands on click. The source preview length lives there too: how many lines to show on each side of the line a stack frame points at, zero for just that line, and the hover card length, the same number for the card that appears while the pointer rests on a frame. The channel pattern is a regular expression whose first group names the channel of a message, `^\[([\w.\- ]{1,64})\]` by default, so `[PlayFabCBSManager] ...` belongs to channel `PlayFabCBSManager`. Changing it re-channels the entries already captured.

## Themes

File > Theme in the toolbar switches the window's look. Native, the default, takes every colour from the Editor's own theme variables, so it matches the light or dark skin and any change Unity makes to it. Obsidian is a dark theme with a blue accent, Paper a warm light one, and Sci-fi a deep navy console with a cyan accent, a faint scanline overlay and a glowing frame around the source preview. The three designed themes set messages, stack frames and source lines in JetBrains Mono, bundled in `Editor/UI/Fonts` under the SIL Open Font License; Native keeps the Editor font. The choice is a per-user preference, not a project setting.

A theme is one USS file in `Editor/UI/Themes` that assigns the `--cc-*` variables `ClarityConsole.uss` reads (background, panel, text, accent, severity colours and so on) and may restyle the built-in controls the window uses. To add one, drop a sheet in that folder, register it in `ConsoleThemes`, and it appears in the menu.

## Reading a stack trace

Selecting an entry lists its stack frames, without the console's own capture frames, with infrastructure runs folded when folding is on in Project Settings, and under every frame that has a file and line, a few lines of that file with the frame's line highlighted, in stack order, so the path the error took reads top to bottom without clicking through the frames. The number of lines on each side is the source preview length in Project Settings; a frame whose file cannot be read gets no block. With folding on, a stack made only of infrastructure folds to a single row for a plain log, since a package's or the engine's logging path is boilerplate, but starts unfolded for an error, exception or assertion, where those frames are the whole story. Rest the pointer on a frame or its block and a card floats up with a longer stretch of the file, fifteen lines by default, formatted the same way; it flips to stay inside the window and disappears when the pointer leaves, the pane scrolls or you click. Its length is the hover card length in Project Settings. Double-clicking a frame or its block opens the file in the code editor. Prefer one preview that follows the frame you click? Turn off "Source under every frame" in the File menu or on the Preferences page.

## Reading in a narrow panel

The Wrap toggle in the toolbar wraps long messages instead of cutting them off, and rows grow to fit, up to about six lines; the full text is always in the detail pane. The severity icon, time and frame stay aligned with the first line, and stack frames wrap as well. Wrap is a per-user preference, off by default, because fixed-height rows are cheaper and most messages fit on one line in a wide window. When the window is too narrow for the toolbar, the search field and the severity toggles move to a second line together rather than being clipped on the right.

The Time and Frame columns are off by default for the same reason: in a docked panel they left little room for the message. Right-click the list header, use File > Columns, or open the Preferences page to show them; the choice is remembered per user and applies to every console window.

## Preferences

Edit > Preferences > Clarity Console gathers the per-user switches in one place: the theme, wrap, source under every frame, the Time and Frame columns, Error Pause and the clear-on options, with a reset to defaults. The same switches are reachable from the toolbar and the File menu; the page follows changes made there. Preferences live in `EditorPrefs` and are never committed with the project, unlike Project Settings > Clarity Console, which holds what a team shares.

## Clearing and pausing

The dropdown beside Clear empties the console when entering Play mode, when a recompile starts, or when a player build starts. Clearing also resets the journal, so nothing comes back after the reload. The Error Pause toggle pauses Play mode as soon as an error, exception or assertion is logged, which freezes the game on the frame that went wrong. These four are per-user preferences in `EditorPrefs`, not project settings: they are a personal habit rather than something a team shares.

## Importing a log file

Drop a `Player.log`, an Editor log or an Android `logcat` dump on the console window, or choose Open log file from the File menu, and it opens in a window of its own. The import is read-only: capture keeps running in the live window, and the imported one is not journaled. Unity logs keep their stack traces, and the `(Filename: ... Line: ...)` suffix becomes a clickable frame; logcat lines keep their level and their tag becomes a channel, so the chips work on a device log too. Severity is only taken from markers the file actually contains, so an import never invents errors that were not there.

## Exporting

The File menu in the toolbar writes the rows currently shown, filters and all, as text, Markdown or JSON, or copies them to the clipboard. Every export starts with a header naming the Unity version, the package version, the session and which filters were active, so a pasted log says where it came from. Right-clicking a row offers the same for a single entry through "Copy for a bug report".

## Watching a value

Logging `[watch:PlayerHP] 87` creates a row that later messages with the same key replace, rather than a new row each time. The row keeps its position, shows the newest value and counts the updates, which makes a per-frame value readable without flooding the list. Every update is still captured and journaled, so searching for an older value finds it. The key pattern is a project setting; emptying it turns watch rows off.

## Ignoring messages

Right-click a row to silence that exact message or its whole channel. Rules are listed in Project Settings, where each can be turned off without deleting it. Ignoring only hides entries from the window: they are still captured and journaled, so removing a rule brings them back, and the status bar always says how many are hidden.

## Where things are stored

| Data | Location | Survives |
|---|---|---|
| Captured entries | `Library/ClarityConsole/journal-*.bin`, segmented, 32 MB budget by default | Domain reload, Play mode, Editor restart, Editor crash up to the last drain |
| Play session counter | `SessionState` | Domain reload; continues from the journal after a restart |
| Project settings | `ProjectSettings/ClarityConsole.asset` | Everything; commit it with the project |
| Preferences (theme, wrap, source blocks, columns, clear-on, Error Pause) | `EditorPrefs` | Everything; per user, not shared |

Clear in the window deletes the journal. Deleting the folder while the Editor is closed has the same effect.

## Working on the package

Open `DevProject~` in Unity 6000.2 or newer, or run the EditMode tests headlessly:

```
Unity -batchmode -nographics -projectPath DevProject~ -runTests -testPlatform EditMode -testResults results.xml
```

The contribution workflow is in [CONTRIBUTING.md](../CONTRIBUTING.md); rules for AI assistants are in [AGENTS.md](../AGENTS.md).
