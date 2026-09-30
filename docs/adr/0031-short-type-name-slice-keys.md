# ADR-0031: Short type-name slice keys

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.1; CORE-01; D1

## Context

The matrix derived keys from namespace + type name, producing noisy DevTools labels (`my-app-cart-cart/Add`) and storage keys that break on namespace refactors.

## Decision

Key = type name (nested: `Outer-Inner`), one `Slice`/`Store`/`Reducer(s)` suffix removed, kebab-cased; overridable; duplicates are DUCKY304; `@ducky/` is reserved for library slices. DUCKY012 suggests pinning keys of persisted slices.

## Consequences

Readable labels, refactor-stable storage; collisions fail loudly at startup.

## Alternatives considered

Namespace-qualified keys (rejected: noise, fragility).
