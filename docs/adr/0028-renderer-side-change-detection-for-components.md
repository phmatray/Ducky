# ADR-0028: Renderer-side change detection for components

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.2; CMP-01; D9; INV-19; review rounds 4 and 5

## Context

Minimal-core and dx-first evaluated component selectors on the drainer, reading `[Parameter]` fields from a thread-pool thread on Server (tearing, races) and deduping against drainer values, which can leave the UI stale after a parameter change.

## Decision

The drainer only schedules. One coalesced `InvokeAsync(Evaluate)` per component (CAS flag, reset first in `Evaluate`; reset with a 1→0 compare-and-swap only when `invokeAsync` fails before `Evaluate` starts, while `Evaluate` catches its own exceptions (review round 4; an unconditional reset could clear a newer commit's flag and allow a second pending `InvokeAsync`, which `Evaluate_SelectorThrows_AtMostOnePendingInvoke` catches); posted with `ExecutionContext.SuppressFlow` and an `await Task.Yield()` inside the suppressed region, so `Evaluate` never runs inline with the drainer's context even when the drainer is already on the renderer's thread) recomputes selections on the renderer and compares with the last value the renderer observed.

## Consequences

No field races; no lost wake-up (property test); no causal scope leaking into render callbacks.

## Alternatives considered

Drainer-side evaluation (rejected: races, stale dedupe).
