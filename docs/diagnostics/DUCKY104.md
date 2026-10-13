# DUCKY104: `[Draftable]` record declares a generated member name

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build error on the record's name (SPEC §13.3).

## What happened

The generator adds a method `Produce`, a method `CreateDraft` and a nested class `Draft` to the record. The name given in
the message is already taken, so the generated code would not compile. Nothing is emitted for the record. The name is
taken when:

- the record declares a member (a property, positional parameter, method or nested type) with it (CS0102);
- the record itself has that name, as in `record Draft(string Subject)`: a member can't be named like its enclosing type
  (CS0542);
- a base type declares a property, field, event or nested type with it that the record can access: the generated member
  would hide it, and the draft would read the inherited property through the generated member. An inherited method
  doesn't count: the generated `Produce` and `CreateDraft` overload it, and `CreateDraft()` takes `new` over a
  parameterless `CreateDraft()`. Neither does an inherited nested type `Draft`, such as the draft of a `[Draftable]` base
  record in another assembly, which the generated `Draft` hides with `new` (SPEC §13.3). A member the record can't access,
  such as a `private` member of its base, is not hidden at all, so it never counts; nor is a generic `CreateDraft<T>()`,
  `Draft<T>()` or nested type `Draft<T>`, which takes no `new`.

Members named `Build`, `IsDirty`, `Revoke` or anything else are fine: the draft implements `IDraft<T>` explicitly, and it
omits its convenience `Build()` when the record has a member named `Build`.

## How to fix it

Rename the record, its member or the base type's member.
