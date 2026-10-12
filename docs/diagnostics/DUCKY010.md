# DUCKY010: Blocking on a Ducky task

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the blocking expression (SPEC §7, §16.2,
ADR-0014).

## What happened

The task of `DispatchAsync`, `SetAsync`, `WhenIdleAsync` or `InitializeAsync` (on `IStore`, `IDispatcher`,
`EffectContext`, `Middleware`, `SliceStore`, `DuckyComponent`, `DuckyLayout` or Ducky.Testing's `TestStore`) is waited on synchronously with `.Result`,
`.Wait()` or `GetAwaiter().GetResult()`, through `ConfigureAwait(...)` too. These tasks complete only once the store's
drainer makes progress: if the caller is the drainer, the wait is a self-deadlock, and Blazor WebAssembly has a single
thread, where such a wait can never complete.

Only a call written in place is seen: storing the task in a variable and waiting on it later is not reported, and
blocks just the same.

## How to fix it

`await` the task. To fire an action without waiting, call `Dispatch` (or `Set` instead of `SetAsync`); to wait for the
store to settle in a test, `await WhenIdleAsync()`.
