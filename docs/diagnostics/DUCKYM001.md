# DUCKYM001: `IState` and `IKeyedAction` removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.IState`, `Ducky.IKeyedAction` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Ducky 2.0 needs no marker interface: any object is a state and any object is an action. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM001)]

## How to fix it

Delete the base type. A state is any record (`sealed record CounterState(int Value);`) and an action is any object, usually a record. A keyed action's key becomes an ordinary property of the record. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
