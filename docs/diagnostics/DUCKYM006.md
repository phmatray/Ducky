# DUCKYM006: The `Fsa` records removed

**Package:** Ducky. **Surface:** error-level `[Obsolete]` on the 1.x `Ducky.Fsa`, `Ducky.Fsa<TPayload>`, `Ducky.Fsa<TPayload, TMeta>`, `Ducky.FsaError`, `Ducky.FsaError<TMeta>`, `Ducky.FsaMeta<TMeta>` (SPEC §22.1, ADR-0038).

## What happened

Your code derives from, implements or calls a Ducky 1.x type that Ducky 2.0 removed. Ducky 2.0 has no flux-standard-action base records: an action is any object. The type survives only as
a member-less tombstone so the compiler reports this one error with a link here, instead of a cascade of CS0246.

[!code-csharp[](../../test/Ducky.Blazor.Tests/Migration/TombstoneTests.cs#DUCKYM006)]

## How to fix it

Declare the action as a plain `record` with no base type; payload, metadata and error become ordinary properties. The migration guide (`docs/migration/ducky-1x.md`) maps every 1.x type. Tombstones are removed in Ducky 3.0.
