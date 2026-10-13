# DUCKY108: `[Draftable]` record is error-obsolete

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build warning on the record's name (SPEC §13.3,
GEN-06).

## What happened

The record is marked `[Obsolete(..., error: true)]`. Generated code that references it would raise CS0619, which can't be
suppressed, so the generator emits no draft for it. Callers of its `Produce` or `CreateDraft` get the compiler's own
errors.

Members marked `[Obsolete(error: true)]` are different: they are left out of the draft and `with` keeps their values.

## How to fix it

Remove `[Draftable]` from the retired record, or make the obsoletion a warning (`[Obsolete("...")]`), which the generated
code tolerates.
