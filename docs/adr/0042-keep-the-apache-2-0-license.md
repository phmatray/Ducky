# ADR-0042: Keep the Apache-2.0 license

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §3; open question 19

## Context

Ducky 1.x and Mutty are Apache-2.0; minimal-core silently proposed MIT.

## Decision

Ducky 2.0 stays Apache-2.0. No Fluxor-derived or EF Core-derived code is carried over. Mutty's own code keeps NOTICE attribution.

## Consequences

No relicensing decision needed; no third-party attribution.

## Alternatives considered

Relicense to MIT (rejected: not an owner decision).
