# DUCKY023: Type excluded from generated registration

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build warning on the type's name (SPEC §16.1, GEN-02).

## What happened

`AddDuckyGenerated_{Asm}` registers every non-abstract, non-generic slice and effect that the generated file can
reference. This one is left out, because generated code referencing it would not compile:

- it is a slice without a public parameterless constructor that `new()` can call: `AddSlice<T>()` requires
  `T : new()`. While the slice or a base type has a `required` member, only a `[SetsRequiredMembers]` constructor
  counts (CS9040);
- it is a `file`-local slice or effect, or is nested in a `file`-local type, so no other file can name it.

Effects need no parameterless constructor: they are created through `ActivatorUtilities`, and their constructor
dependencies are checked when the store is first resolved (DUCKY309).

## How to fix it

For a slice without a usable constructor, the only fix is a public parameterless constructor (marked
`[SetsRequiredMembers]` if the slice has `required` members): `AddSlice<T>()`, the only slice registration API, requires
`T : new()` too.

For a `file`-local type, make it non-`file`, or register it yourself from inside the same file: `builder.AddSlice<T>()`
for a slice, `builder.AddEffect<T>()` for an effect. The warning is reported on the type's name, so
`#pragma warning disable DUCKY023` around the declaration silences it there.
