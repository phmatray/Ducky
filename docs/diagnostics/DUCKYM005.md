# DUCKYM005: `MemoizedSelector` removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.MemoizedSelector` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Memoized selectors are defined with `Selector.Define` and instantiated per consumer. The CS0117 on the 1.x static call disappears with the call. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM005)]

## How to fix it

Replace `MemoizedSelector.Create(...)` with `Selector.Define(...)` and create an instance with `def.Create()`. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
