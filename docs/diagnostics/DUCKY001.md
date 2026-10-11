# DUCKY001: Non-deterministic call in a reducer or projector

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the call (SPEC §16.2).

## What happened

A reducer passed to `On<T>` or a projector passed to `Selector.Define` reads a value that changes from one call to the
next: `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`,
`Guid.NewGuid`, any member of `Random` (`Random.Shared`, `new Random()`, `Next`…), `Environment.TickCount`,
`Environment.TickCount64`, any member of `Stopwatch`, or `TimeProvider.GetUtcNow` (an override such as
`FakeTimeProvider.GetUtcNow` included). Each use is reported, so `Random.Shared.Next()` gets two warnings.

Reducers and projectors must be pure: the same state and action always give the same result. A reducer that reads
the clock can't be replayed by Redux DevTools or tested with a fixed input, and a projector is memoized on its inputs,
so a non-deterministic result is cached and may be computed twice by racing readers (SPEC §6.9).

Only the lambda written directly as the `On<T>` reducer or the `Define` projector is analysed, nested lambdas inside
it included. A method group, a delegate stored in a variable, effects and generated code are not analysed.

## How to fix it

Read the value where side effects belong and carry it in the action: an effect (`ctx.Time.GetUtcNow()`) or the
component that dispatches, as in `Dispatch(new Added(item, time.GetUtcNow()))`, and let the reducer use `a.At`. For a
projector, pass the value as the selector argument (`Selector.Define(in1, (x, now) => …)`). `ctx.Time` is the store's
`TimeProvider`, so tests drive it with `FakeTimeProvider`.
