# DUCKYM008: `DuckyLayout<TState>` removed

**Package:** Ducky.Blazor. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.Blazor.DuckyLayout<TState>` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. The generic layout is replaced by the non-generic `DuckyLayout`, which this tombstone coexists with. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM008)]

## How to fix it

Derive from the non-generic `DuckyLayout` and read the state with `Select` in `OnInitialized`. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
