# ADR-0033: Discard 1.x persisted data

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.5; owner decision; open question 15

## Context

1.x persisted with AssemblyQualifiedName and no version.

## Decision

No migration reader. The new envelope `{v, at, s}` fails to parse 1.x data, which is discarded with a Debug log.

## Consequences

Users lose locally persisted 1.x state once on upgrade (documented in the 1.x guide).

## Alternatives considered

One-time reader (rejected by the owner: type-injection hazard, dead code).
