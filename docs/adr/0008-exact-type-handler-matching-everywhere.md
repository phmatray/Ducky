# ADR-0008: Exact-type handler matching everywhere

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.1, §6.6; CORE-01, EFF-01; matrix conflict 5; open question 7

## Context

1.x reducers matched the exact type but `AsyncEffect<T>` matched subtypes; NotRedux walked base types.

## Decision

Reducers, effects, effect groups and `OfActionType` match `action.GetType()` exactly. Duplicate handlers throw at construction (DUCKY014 at compile time). Abstract/interface/`object` handlers are DUCKY005 errors and DUCKY307 at runtime. Polymorphic matching is deferred.

## Consequences

One `FrozenDictionary` lookup per slice; no ambiguity about which handler wins. 1.x `AsyncEffect<TBase>` users must register concrete types.

## Alternatives considered

Base-type walking (rejected: order ambiguity and cost); `is T` for effects only (rejected: inconsistent).
