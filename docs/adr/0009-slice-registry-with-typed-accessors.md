# ADR-0009: Slice registry with typed accessors

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.2, §6.2; matrix conflict 6

## Context

Ducky used a dynamic slice registry; DuckyNext generated a typed root state.

## Decision

A registry frozen at store build, read through `State.Get<TState>()`, `TryGet`, `Get(key)`, `WasRestored`. Each state type maps to exactly one slice (DUCKY305/DUCKY015).

## Consequences

Works across assemblies without a whole-program generator; AOT-safe; no generated root that collides across projects.

## Alternatives considered

Generated typed root (rejected: cross-assembly generation, collisions, GEN-04 family).
