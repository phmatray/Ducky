# ADR-0041: Branch protection for a solo maintainer

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §20.2; ENG-01

## Context

Correctness-first required one CODEOWNERS approval, which blocks every merge on a solo-maintainer repo.

## Decision

`v2` is the repository's default branch during 2.0 development, because GitHub runs `schedule` workflows only from the default branch (review round 3); `main` becomes the default again at the GA rename. A checked-in idempotent `gh api` script: PR required, 0 approvals, strict required checks named exactly after the reporting jobs (`ci`, `ci-cross-windows`, `ci-cross-macos`, `e2e`, `aot`, `mutation`, `pr-title`), linear history, squash only, admins included, a `v*` tag ruleset limited to the maintainer (reachability from the protected branch is checked by `Publish`, since rulesets can't express it).

## Consequences

Gates are enforced by CI, not by reviewers; reproducible configuration.

## Alternatives considered

Required approval (rejected: blocks merges or needs admin bypass).
