# ADR-0005: One core effect shape

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.5, §6.6; EFF-01/03/06; matrix conflict 2; review round 7

## Context

Siblings used effect classes, reducer-returned commands, `IAsyncEnumerable` streams and Rx epics.

## Decision

Core has one shape: `Effect<TAction>` (plus `EffectGroup`, EFF-01's multi-action group: several action types in one DI class) with a keyable `Merge/Switch/Exhaust/Queue` policy and store-lifetime cancellation. Rx lives in the optional Ducky.Reactive. Streams and commands-as-data are deferred.

## Consequences

One runner and one test matrix (4 policies × keys × outcomes). Users needing streams use Ducky.Reactive.

## Alternatives considered

Several core shapes (rejected: coherence and test cost); Rx in core (rejected: dependency in core).
