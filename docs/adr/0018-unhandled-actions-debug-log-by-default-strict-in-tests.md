# ADR-0018: Unhandled actions: Debug log by default, strict in tests

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.4 step 12; CORE-08; matrix conflict 15

## Context

An action that nothing handles is usually a typo, but Rx effects and custom middleware are opaque to the check.

## Decision

Log at Debug by default. `ThrowOnUnhandledAction` turns it into `ReducerFailed(UnhandledActionException)`; `TestStore` enables it.

## Consequences

No false-positive failures in production; tests catch typos.

## Alternatives considered

Throw by default (rejected: opaque consumers).
