# ADR-0017: Per-assembly generated registration names

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §16.1; GEN-02, GEN-07; matrix conflict 14; open question 10

## Context

A single shared method name across assemblies produced CS0121 (reproduced). Auto-aggregation needs cross-compilation discovery.

## Decision

Emit `Ducky.Generated.{Asm}_DuckyRegistrations.AddDuckyGenerated_{Asm}()` with a table-tested sanitizer (every non-identifier character becomes `_`; names that differ only in such characters collide, which is documented rather than hashed). DUCKY016 flags a forgotten call. Auto-aggregation is deferred (GEN-07).

## Consequences

Collision-proof (multi-assembly fixture, INV-25); names are verbose.

## Alternatives considered

One global name (rejected: CS0121); auto-aggregation now (rejected: incrementality cost).
