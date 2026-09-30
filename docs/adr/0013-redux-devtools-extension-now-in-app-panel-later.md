# ADR-0013: Redux DevTools extension now, in-app panel later

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.8; DT-01, DT-03; matrix conflict 10; open question 20

## Context

Existing in-app panels leak state across circuits or rely on reflection.

## Decision

2.0 integrates the Redux DevTools browser extension with working time travel over typed snapshots, real sanitizers and wired options. An in-app panel is a post-GA optional package.

## Consequences

Users get the tool they expect; no cross-circuit history.

## Alternatives considered

Ship both (rejected: scope); panel only (rejected: less capable).
