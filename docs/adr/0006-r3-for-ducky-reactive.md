# ADR-0006: R3 for Ducky.Reactive

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §14; EFF-04; matrix conflict 3; open question 3; review rounds 5 and 6

## Context

The published 1.0.292 used R3, the unreleased dev branch switched to System.Reactive. One library must be chosen.

## Decision

Use R3 1.3.x, pinned exactly in `Directory.Packages.props` (a plain version, never a bracketed range) and the lock file, and packed as the minimum dependency `R3 >= 1.3.x`, so a consumer that also references a newer R3 gets no NU1107/NU1608 (review round 6; PackageSmoke asserts that no dependency uses a bracketed exact range). Only `OfActionType` is added. Errors use R3's non-terminal `OnErrorResume`; failure completion resubscribes with backoff on `TimeProvider`. The bridge's state is R3's `SynchronizedReactiveProperty<T>`, because subscriptions change on timer, pool and disposing threads while the drainer writes values (review round 5).

## Consequences

AOT/trim-friendly, native `TimeProvider` support, no second scheduler abstraction, and error resumption that fixes the 1.x 'one error kills the stream' bug. System.Reactive must never enter the restore graph (R-PKG-1).

## Alternatives considered

System.Reactive (rejected: owner decision, heavier, scheduler-based time).
