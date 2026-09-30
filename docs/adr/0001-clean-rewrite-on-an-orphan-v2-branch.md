# ADR-0001: Clean rewrite on an orphan v2 branch

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §18, §24; open question 2

## Context

Ducky 1.x dev does not restore as committed, has no CI, 59 IL warnings, and a design (RW lock, Monitor.Wait, untyped hydrate) that the matrix replaces almost entirely. The owner chose a clean rewrite.

## Decision

Develop 2.0 on `git switch --orphan v2` in phmatray/Ducky. Port concepts, never files. 1.x stays on `main` until GA, then moves to `release/1.x`; `v2` becomes `main`. Nothing from the `wip/local-only-*` branches is carried over.

## Consequences

History of 2.0 starts clean and every line is born behind the gates. MinVer needs `MinVerMinimumMajorMinor=2.0` (no reachable `v*` tag). 1.x fixes, if any, happen on `release/1.x`.

## Alternatives considered

Evolve `dev` (rejected: every gap investigator hit restore failures and the architecture changes anyway); a new repository (rejected: loses stars, issues and NuGet linkage).
