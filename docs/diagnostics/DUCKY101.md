# DUCKY101: `[Draftable]` type is not a partial record class

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build error on the type's name (SPEC §13.3).

## What happened

The Draft generator adds `Produce`, `CreateDraft` and a nested `Draft` class to the `[Draftable]` type itself, which only
works for a `partial record class` that is not file-local. It emits nothing for this type, because it is:

- not `partial`, so the generated part can't join it;
- a `record struct` (the compiler also reports CS0592): a struct has no reference identity, so a no-op recipe can't
  return "the same instance";
- a plain class, not a record: the draft's `Build` relies on `with`;
- `file`-local, or nested in a `file`-local type: a file-local type can't take a partial part from another file, so the
  generated part would declare a different type.

A parent record with a member of this type still gets its draft; that member is a plain get/set.

## How to fix it

Declare the type as `[Draftable] public partial record MyState(...)`. For a record struct, make it a `record class`, or
keep editing it with `with`. For a file-local type, drop `file` (an `internal` type stays out of the public API).
