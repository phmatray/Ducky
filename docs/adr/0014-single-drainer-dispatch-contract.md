# ADR-0014: Single-drainer dispatch contract

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.3–6.4, §7; CORE-02; matrix conflict 11; open question 6

## Context

1.x gave read-your-writes to every caller by blocking with `Monitor.Wait`, which deadlocks. The single-drainer rule never blocks, but a queued caller returns before its action is reduced.

## Decision

Any thread enqueues under one `Lock`; the idle caller drains inline on its own context; empty check and release share one critical section. `DispatchAsync` completes after reduce (never inline, never faulting). 'After effects' is `WhenIdleAsync` / `TestStore.Settled`.

## Consequences

No deadlock (INV-05), no stranded action (INV-03), global FIFO (INV-04). Sync `Dispatch` from another thread is weaker than 1.x (documented, DUCKY010 against blocking).

## Alternatives considered

Keep blocking (rejected: deadlock reproduced); make `DispatchAsync` wait for effects (rejected: unbounded, and effects may never finish).
