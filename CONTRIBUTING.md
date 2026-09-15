# Contributing to Clarity Console

This document is the workflow contract for every change, whether written by hand or with an AI assistant.

## First-time setup

```
git config core.hooksPath .githooks
```

The hooks reject commit messages that are not Conventional Commits and refuse direct pushes to `main`. They are a courtesy check; the same rules are enforced on GitHub.

## Branching model

- `main` is always releasable and is protected: pull requests only, linear history, required checks, every review conversation resolved.
- Every change lives on a short-lived branch cut from an up-to-date `main`:
  `feat/<topic>`, `fix/<topic>`, `docs/<topic>`, `chore/<topic>`, `ci/<topic>`, `refactor/<topic>`, `perf/<topic>`, `test/<topic>`.
- One branch per issue. Branches are deleted automatically on merge.

## Issues first

- Features and bugs start as an issue using the templates. Small chores may skip the issue.
- Labels: one `area: *`, one `phase: *`, one `priority: *`, plus the kind (`bug`, `enhancement`, `documentation`, `chore`, `performance`).
- Milestones map to releases: `v0.1.0`, `v0.5.0`, `v1.0.0`.

## Commits

- [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): subject`.
  - Types: `feat`, `fix`, `docs`, `chore`, `ci`, `refactor`, `perf`, `test`, `build`, `revert`.
  - Scopes: `core`, `capture`, `ui`, `settings`, `extensions`, `tests`, `samples`, `docs`, `ci`, `release`, `deps`.
- Subject in the imperative mood, lowercase first letter, no trailing period, at most 72 characters. The body explains why, not what.
- Breaking changes: `feat(extensions)!: ...` plus a `BREAKING CHANGE:` footer.

## Pull requests

- Target `main` and fill in the template. Keep it a draft while work is in progress.
- The PR title must itself be a valid Conventional Commit; the `PR title` check enforces it and the squash merge uses it as the commit subject with the PR body as the message.
- Link the issue with `Closes #123`.
- Before requesting review: self-review the diff, run the EditMode tests locally, add a `CHANGELOG.md` line under `[Unreleased]` (or apply `skip-changelog`), and update docs if behaviour changed.
- Squash merge only. Never merge with a failing check.

## Changelog

[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Every user-visible change adds a line under `[Unreleased]` in one of Added, Changed, Deprecated, Removed, Fixed, Security.

## Releases

- [Semantic Versioning](https://semver.org/). `MAJOR` for breaking changes to the public `ClarityConsole.Extensions` API or to settings files, `MINOR` for features, `PATCH` for fixes.
- A release PR titled `chore(release): vX.Y.Z` bumps `package.json`, turns `[Unreleased]` into a dated section and updates the comparison links.
- After it merges, tag `vX.Y.Z` on `main`. The release workflow checks that the tag matches `package.json` and publishes a GitHub Release with that changelog section. OpenUPM picks the tag up automatically.

## Code standards

- Unity floor is the `unity` field in `package.json` (`2022.3`). Newer APIs go behind version defines and are exercised by the CI matrix.
- Zero third-party dependencies in the package. Optional integrations only through asmdef version defines.
- Public API lives only in `ClarityConsole.Extensions`; everything else is `internal`.
- Logic in `ClarityConsole.Core` ships with EditMode tests. Stack-trace fixtures come from real Mono and IL2CPP output.
- Formatting follows `.editorconfig`: Allman braces, four spaces, LF, UTF-8 without BOM.

## Working with AI assistants

The same rules apply. Assistants never push to `main`, never force-push, and open pull requests like any other contributor. The binding instructions for every assistant are in `AGENTS.md`; `CLAUDE.md` and `.github/copilot-instructions.md` defer to it.
