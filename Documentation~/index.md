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

## Settings

Project Settings > Clarity Console holds the settings a team shares through version control. Today that is the channel pattern: a regular expression whose first group names the channel of a message, `^\[([\w.\- ]{1,64})\]` by default, so `[PlayFabCBSManager] ...` belongs to channel `PlayFabCBSManager`. Changing it re-channels the entries already captured.

## Where things are stored

| Data | Location | Survives |
|---|---|---|
| Captured entries | `Library/ClarityConsole/journal-*.bin`, segmented, 32 MB budget by default | Domain reload, Play mode, Editor restart, Editor crash up to the last drain |
| Play session counter | `SessionState` | Domain reload; continues from the journal after a restart |
| Project settings | `ProjectSettings/ClarityConsole.asset` | Everything; commit it with the project |

Clear in the window deletes the journal. Deleting the folder while the Editor is closed has the same effect.

## Working on the package

Open `DevProject~` in Unity 6000.2 or newer, or run the EditMode tests headlessly:

```
Unity -batchmode -nographics -projectPath DevProject~ -runTests -testPlatform EditMode -testResults results.xml
```

The contribution workflow is in [CONTRIBUTING.md](../CONTRIBUTING.md); rules for AI assistants are in [AGENTS.md](../AGENTS.md).
