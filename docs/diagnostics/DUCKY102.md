# DUCKY102: Containing type of a `[Draftable]` record is not partial

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build error on the record's name (SPEC §13.3).

## What happened

The `[Draftable]` record is nested, and the generated part must repeat every containing type declaration. The type named
in the message is not `partial`, so the generated declaration would be a second, conflicting type. Nothing is emitted for
the record.

## How to fix it

Add `partial` to every containing type (`public static partial class Features { [Draftable] public partial record ... }`),
or move the record to namespace level.
