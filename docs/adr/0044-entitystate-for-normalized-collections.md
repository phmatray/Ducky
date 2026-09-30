# ADR-0044: EntityState for normalized collections

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.10; IMM-01

## Context

Correctness-first silently dropped IMM-01, a core matrix item used by the demos; minimal-core cut Map/Merge.

## Decision

Ship `EntityState<TKey,TEntity>` with `IEntity<TKey>`, insertion order, a lazily cached frozen index, Add/Upsert/Update/Map/Remove/RemoveWhere/SetAll/Merge(MergeStrategy), no-op returns `this`, and JSON serialization of `Items` only.

## Consequences

Model-based property test against Dictionary + ordered List (INV-32).

## Alternatives considered

Port 1.x `NormalizedState` record as-is (rejected: recomputed `AllIds`, lost insertion order).
