# DUCKYM007: `NormalizedState<TKey, TEntity, TState>` removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.NormalizedState<TKey, TEntity, TState>` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Normalized entities are held by an `EntityState<TKey, TEntity>` value instead of a state base type. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM007)]

## How to fix it

Drop the base type and give the state record an `EntityState<TKey, TEntity>` member. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
