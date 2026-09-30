# ADR-0047: A newer envelope version is treated as not found

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §2.2 D18, §11.5, §11.7; PER-01; INV-14, INV-18

## Context

After a rollback, or in a stale tab still running the previous deploy, storage can hold an envelope whose `v` is greater than the slice's `Version`. Migrations only run forward.

## Decision

On hydration and on cross-tab receipt (one shared envelope reader), `v > Version` is treated as not found and logged at Warning. The slice keeps its initial or current state; the next local change overwrites the newer entry.

## Consequences

No deserialization of a shape the code doesn't know; a rolled-back deploy loses the newer data for that slice (documented). `Persist_Envelope_Migrations_Ttl_NewerVersionIgnored`, `CrossTab_NewerVersionEnvelope_Ignored`, `Persist_EnvelopeRoundTrip_Property`.

## Alternatives considered

Best-effort deserialization of the newer shape (rejected: silent data corruption); refusing to start (rejected: one stale key would break the app).
