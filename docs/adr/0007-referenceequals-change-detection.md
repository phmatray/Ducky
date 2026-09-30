# ADR-0007: ReferenceEquals change detection

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.3, §6.4; CORE-01; matrix conflict 4

## Context

1.x compared states with `Equals` (deep record equality on every dispatch). Mutty's `Produce` always returned a new instance, so reducers built on it notified on every action.

## Decision

A slice changed iff the reducer returned a different reference. `TState : class` is required. DUCKY011 flags `s with { }`. Ducky.Draft returns the original reference for a no-op and keeps untouched branches reference-equal (INV-26).

## Consequences

O(1) change detection and memoization by reference. A new-but-equal record now notifies (documented breaking change).

## Alternatives considered

Keep `Equals` (rejected: cost, and value types/records hide real changes in collections); hybrid comparer per slice (rejected: complexity).
