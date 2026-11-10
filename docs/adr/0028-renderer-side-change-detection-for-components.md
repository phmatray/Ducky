# ADR-0028: Renderer-side change detection for components

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.2; CMP-01; D9; INV-19; review rounds 4 and 5; amended 2026-11-10 (BLAZOR-RERENDER)

## Context

Minimal-core and dx-first evaluated component selectors on the drainer, reading `[Parameter]` fields from a thread-pool thread on Server (tearing, races) and deduping against drainer values, which can leave the UI stale after a parameter change.

## Decision

The drainer only schedules. One coalesced `InvokeAsync(Evaluate)` per component (CAS flag, reset first in `Evaluate`; reset with a 1→0 compare-and-swap only when `invokeAsync` fails before `Evaluate` starts, while `Evaluate` catches its own exceptions (review round 4; an unconditional reset could clear a newer commit's flag and allow a second pending `InvokeAsync`, which `Evaluate_SelectorThrows_AtMostOnePendingInvoke` catches); posted with `ExecutionContext.SuppressFlow` and an `await Task.Yield()` inside the suppressed region, so `Evaluate` never runs inline with the drainer's context even when the drainer is already on the renderer's thread) recomputes selections on the renderer and compares with the last value the renderer observed.

## Consequences

No field races; no lost wake-up (property test); no causal scope leaking into render callbacks.

## Alternatives considered

Drainer-side evaluation (rejected: races, stale dedupe).

## Amendment (2026-11-10): schedule under an empty ExecutionContext

`ExecutionContext.SuppressFlow` around the `InvokeAsync` and its `Task.Yield` posted the check with *no* `ExecutionContext`,
not an empty one, and such a continuation runs under the context of whichever thread runs it. Posted behind an incomplete
renderer work item, it runs inline on the thread that completes the item, when that thread's synchronization context is null
or the default one: the drainer itself when the renderer was idle (the check and its render then ran inside the drain, under
the drainer's causal scope, against INV-19), or an outside caller of `InvokeAsync(Func<Task>)`, which completes its item
after restoring its own synchronization context. bUnit's event dispatch is such a caller; on Blazor Server, circuit start,
root component updates and location changing are, while browser events and location changed use `InvokeAsync(Action)`,
which completes its item before restoring the caller's context. ASP.NET Core's `RendererSynchronizationContext.PostAsync`
(10.0.14, unchanged on main) sets itself as current around posted work and relies on the thread pool, or on the
continuation's own `ExecutionContext.RunInternal`, to undo it; inline and context-less, nothing did, and that caller stayed
on the renderer's context, where `Dispatcher.CheckAccess()` passes outside the renderer's queue. In bUnit the caller is the
test thread: its later calls ran renderer work concurrently with renders on other threads, so a wait missed its render and a
component could stop re-rendering for good (the docs tutorial's badge after a blocking `Click()`, about one run in 25, and
every run when the test thread advanced a fake clock that resumed an effect).

The change check is now scheduled with `ExecutionContext.Run` under an empty context: `ExecutionContext.Default`, which is not
public, captured once per process as `ExecutionContext.Capture()` on a thread started with `Thread.UnsafeStart()` (no flow).
Everything the check posts carries that context, so no `AsyncLocal` reaches `Evaluate` wherever it runs, `RunInternal`
restores the thread it runs on, and `Run` gives the drainer back its own context. The check's culture is the running
thread's, as before. Where no thread can start (single-threaded WebAssembly, WASI) there is no empty context and the check is
scheduled with flow suppressed, as before; the renderer there dispatches inline (`NullDispatcher`), so nothing posted runs on
another caller's thread. The `Task.Yield` stays: it still runs `Evaluate` after the current work item when `InvokeAsync` runs
the delegate inline.

Rejected:
- the context captured when the core is created: it would hold the creator's `AsyncLocal`s, and a component created on an
  effect continuation's flow would render, for its whole life, under that run's stale cause (its dispatches joining an old
  chain) and run token (its `SliceStore` writes dropped once the run is superseded, `WhenIdleAsync` throwing);
- a hook from Ducky that scrubs the store's `AsyncLocal`s: the first cross-package internals coupling, and it could not reach
  every store;
- a thread-pool hop before `InvokeAsync`: it gives an empty context, but the drain would no longer post the check, so a
  caller could no longer settle on it with one `InvokeAsync`.
