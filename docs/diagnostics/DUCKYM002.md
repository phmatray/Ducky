# DUCKYM002: `SliceReducers<TState>` and `ISlice<TState>` removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.SliceReducers<TState>`, `Ducky.ISlice<TState>` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Slices are declared by deriving from `Slice<TState>`. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM002)]

## How to fix it

Write `sealed class X : Slice<TState>` and give it `protected override TState Initial =>` instead of `GetInitialState()`. The CS0115 errors on the 1.x overrides disappear with the base type. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
