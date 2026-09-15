# ADR-0004: GitHub flow with tags, no development branch

- Status: accepted
- Date: 2026-09-15

## Context

The maintainer's habit from game development is git-flow: a permanent `dev` integration branch promoted to `main` at release time. That model fits products with discrete store releases and a live version needing hotfixes. This package is a versioned library maintained by one person, with CI on every pull request.

## Decision

`main` is the only long-lived branch and is protected for everyone, administrators included. Every change is a short-lived `type/topic` branch merged by squash through a pull request whose title is a Conventional Commit. Releases are tags on `main`. A `release/x.y` branch is cut from a tag only when an older version needs a backport, and deleted afterwards.

## Consequences

- Each change merges once, and `main` is always the state CI last validated.
- A git URL install without a tag suffix resolves to unreleased work; install instructions therefore always pin `#vX.Y.Z`.
- External contributors target the default branch, which is what they expect.

## Alternatives considered

- Permanent `dev` branch: rejected, it doubles merges and adds a promotion step that buys nothing when every pull request is already validated.
- Release-only `main` with a `next` branch: deferred until two version lines must be maintained at once.
