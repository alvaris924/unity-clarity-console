# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Console window under `Window > Clarity Console`: a virtualized multi-column list bound to the capture store, severity toggles with live counts, case-insensitive search, exact-message collapse with counts, a detail pane with the message and raw stack trace, auto-scroll that pauses while you read older entries, and a status bar.
- Log capture: a wrapping log handler plus the threaded callback record every Editor log with its context object, thread and frame into `LogStore`, drained on the Editor update tick under a time budget, with Play mode and domain-load markers and a persisted Play session counter.
- Package scaffold: `package.json`, the six assembly definitions, `RingBuffer<T>` in `ClarityConsole.Core` with EditMode tests, the `DevProject~` development project, the CI test matrix and the tag-driven release workflow.
- Repository workflow: contribution guide, `AGENTS.md` rules for AI assistants, local git hooks, pull request and issue templates, Conventional Commit title check, Dependabot for GitHub Actions.

[Unreleased]: https://github.com/alvaris924/unity-clarity-console/commits/main
