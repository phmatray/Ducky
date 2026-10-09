# DUCKYM003: `AsyncEffect<TAction>` and `IAsyncEffect` removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.AsyncEffect<TAction>`, `Ducky.IAsyncEffect` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Effects derive from `Effect<TAction>`. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM003)]

## How to fix it

Derive from `Effect<TAction>`, take dependencies through constructor injection and implement `Handle(action, EffectContext, CancellationToken)`. Dispatch with `ctx.Dispatch` and read the state with `ctx.State`. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
