# Clarity Console

Fast, searchable, dependency-free Console window for the Unity Editor.

> **Status: pre-release.** Live log capture and the first window are in: a virtualized list with severity toggles, search, collapse, a detail pane with clickable stack frames that open your code editor, context-object ping, channel chips that group `[Tag]` messages, and a journal that keeps entries across domain reloads and Editor restarts. Error pause and log file import come next; follow the [milestones](https://github.com/alvaris924/unity-clarity-console/milestones).

## What it will do

- A virtualized log list that stays smooth at 100k entries
- A small query language: `sev:error tag:Net* -"retry scheduled" /timeout \d+ms/i`
- Channel chips derived from `[Tag]` prefixes, smart collapse, pins, unread badge
- Clickable stack frames with de-noising and an inline source preview of the failing line
- Log history that survives domain reloads and Editor restarts
- Compiler messages in a Problems tab, session markers, log file import
- Public Unity APIs only, UI Toolkit, zero third-party dependencies, MIT

## Requirements

Unity 2022.3 LTS or newer. Development happens on Unity 6000.2; the CI matrix in `.github/workflows/ci.yml` lists every version the tests run on.

## Install

Not published yet. Once `v0.1.0` is tagged, add the package from its git URL in the Package Manager:

```
https://github.com/alvaris924/unity-clarity-console.git#v0.1.0
```

or through OpenUPM:

```
openupm add com.alvaris.clarity-console
```

## Roadmap

| Version | Scope |
|---|---|
| v0.1.0 | Log capture with context objects, virtualized list, search, collapse, detail pane, session markers, clear options, preferences |
| v0.5.0 | Query language, channel chips, smart collapse, source preview, frame de-noising, pins, shortcuts, export, Problems tab, journal persistence, multiple windows |
| v1.0.0 | Timeline strip, watch rules, log file import, session history, extension API, benchmarks |

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) first. AI assistants follow [AGENTS.md](AGENTS.md). Design notes and decision records live in [Documentation~](Documentation~/index.md).

## License

[MIT](LICENSE.md)
