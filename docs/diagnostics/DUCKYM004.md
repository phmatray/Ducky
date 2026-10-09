# DUCKYM004: `IReactiveEffect` removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.IReactiveEffect` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Reactive effects moved to the Ducky.Reactive package. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM004)]

## How to fix it

Reference Ducky.Reactive and derive from its `ReactiveEffect`, which works on an R3 `Observable<object>` of actions. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
