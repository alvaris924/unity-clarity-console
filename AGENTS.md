# AGENTS.md

Binding rules for every AI coding assistant that works in this repository: Claude Code, OpenAI Codex and ChatGPT agents, GitHub Copilot, Cursor, Gemini CLI or anything else. `CLAUDE.md` and `.github/copilot-instructions.md` defer to this file. If this text was pasted into a chat, treat it as binding for anything produced for this repository.

## The project in one paragraph

Clarity Console is an open-source, dependency-free Console window replacement for the Unity Editor, built on UI Toolkit and shipped as the UPM package `com.alvaris.clarity-console`. The package sits at the repository root; the development Unity project lives in `DevProject~/`, which UPM consumers never see. Assemblies: `ClarityConsole.Core` (pure C#, no engine references), `ClarityConsole.Capture`, `ClarityConsole.UI`, `ClarityConsole.Settings`, `ClarityConsole.Extensions` (the only public API) and `ClarityConsole.Tests`. Unity floor 2022.3. License MIT. Design notes and decision records live in `Documentation~/`.

## Workflow, non-negotiable

1. **Ask before every git action.** Never commit, push, tag, merge or open a pull request without the user's explicit go-ahead in the current conversation turn. Approval given earlier does not carry over.
2. **Never touch `main` directly.** No direct pushes, no force-pushes, no rewriting of pushed history. `main` is protected on the server for administrators too; do not look for a way around it.
3. **One route for every change.** Update `main`, cut a branch named `type/topic` (`feat/`, `fix/`, `docs/`, `chore/`, `ci/`, `refactor/`, `perf/`, `test/`), commit, push the branch, open a pull request against `main` using `.github/PULL_REQUEST_TEMPLATE.md`, wait for the `PR title` check, report the PR URL. Squash merge only, and only with green checks.
4. **Conventional Commits everywhere.** Commit messages and PR titles are `type(scope): subject`. Types: feat, fix, docs, chore, ci, refactor, perf, test, build, revert. Scopes: core, capture, ui, settings, extensions, tests, samples, docs, ci, release, deps. Subject in lowercase imperative, no trailing period, header under 72 characters. Breaking changes use `type(scope)!:` plus a `BREAKING CHANGE:` footer.
5. **Local hooks stay on.** Once per clone run `git config core.hooksPath .githooks`. The hooks reject non-conforming commit messages and pushes to `main`. Never use `--no-verify`.
6. **Changelog and docs move with the code.** Every user-visible change adds a line to `CHANGELOG.md` under `[Unreleased]`; otherwise the PR gets the `skip-changelog` label. `README.md` and `Documentation~/` change whenever behaviour or public API changes.
7. **Issues first.** Features and bugs start as issues with one `area:`, one `phase:` and one `priority:` label and a milestone. PRs reference them with `Closes #N`.
8. **Releases are deliberate.** A PR titled `chore(release): vX.Y.Z` bumps `package.json` and dates the changelog section. After it merges, `vX.Y.Z` is tagged on `main`. Nothing else is ever tagged.

## Engineering rules

- Zero third-party dependencies in the package. Only built-in modules and Unity-registry packages may appear in `package.json`. Optional integrations go behind asmdef version defines.
- Public Unity APIs only in the default path. No reflection into `UnityEditor` internals, no Harmony patching.
- Unity floor 2022.3. Newer APIs are version-guarded (`#if UNITY_6000_0_OR_NEWER`) and covered by the CI matrix.
- Public API lives only in `ClarityConsole.Extensions`. Everything else is `internal`, with `InternalsVisibleTo` for tests.
- Hot-path discipline: the threaded log callback only enqueues. No parsing, no Unity object access, no allocation beyond the entry itself.
- Editor-only assemblies. `[InitializeOnLoad]` and `ScriptableSingleton` are the sanctioned static entry points; avoid other static state.
- UI Toolkit element trees are built in C# and styled with USS. No UXML custom-element registration, so 2022.3 and 6000.x share one code path.
- Logic in `ClarityConsole.Core` ships with EditMode tests. Stack-trace fixtures come from real Mono and IL2CPP output.
- Style per `.editorconfig`: Allman braces, four spaces, LF, UTF-8 without BOM, `_camelCase` private fields, C# 9 as Unity compiles it (no records, no init-only setters).
- Clean room: never read, decompile or copy code from proprietary plugins, and never reproduce their UI text, icons or docs. Features come from public Unity APIs and this project's own design. If such a plugin is present in the host project, do not open its files while working on this package.

## Definition of done for a pull request

CI green on every Unity version in the matrix, tests added or updated, changelog line present or `skip-changelog` applied, docs updated, self-review done, every item of the PR template checklist ticked.

## Commands

```
git config core.hooksPath .githooks                 # once per clone
unity test DevProject~ --mode EditMode              # EditMode tests with the Unity CLI, or use the Test Runner window
gh pr create --base main --title "type(scope): subject" --body-file .github/PULL_REQUEST_TEMPLATE.md
```
