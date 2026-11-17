# DUCKY031: Hand-written RegisterCases on a generated union-first type

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the slice's or effect group's name (ADR-0051).

## What happened

With the generator on, it writes the `RegisterCases` override of every `Slice<TState, TAction>` and
`EffectGroup<TAction>`, one `On<Case>` per case of the union. This type declares a `RegisterCases()` of its own, so the two
members would collide (CS0111). The generator writes nothing for it, and reports this instead.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY031)]

## How to fix it

Remove the hand-written `RegisterCases()` and let the generator register every case. A hand-written `RegisterCases` is for a build
without the Ducky generator; for a policy or key per case,
derive from `EffectGroup` and call `On<TCase>` instead.
