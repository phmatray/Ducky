# DUCKY030: Union-first type is file-local

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the slice's or effect group's name (ADR-0051).

## What happened

The generator registers the cases of a `Slice<TState, TAction>` or an `EffectGroup<TAction>` by overriding
`RegisterCases` in a generated `partial` declaration of the type, in a file of its own. A `file` type can't span two
files, so when the type, or a type it is nested in, is declared `file`, no generated declaration can extend it and no
case is registered. This replaces the DUCKY023 its registration would otherwise get.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY030)]

## How to fix it

Remove the `file` modifier the message names; `internal` keeps the type out of other assemblies.
