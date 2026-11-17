# DUCKY032: Union case is obsolete as an error

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the slice's or effect group's name (ADR-0051).

## What happened

The generator writes the `RegisterCases` override of every `Slice<TState, TAction>` and `EffectGroup<TAction>`, one
`On<Case>` per case of the union, which names each case, the unions that wrap it and the constructors those wraps call. One
of them carries `[Obsolete(…, true)]`, or sits in a type that does, and CS0619 can't be suppressed, so the override would not
compile. The generator writes nothing for this type, and reports this instead, naming what is obsolete. A union declared in
an obsolete context may list such a case; the generated override is not in that context.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY032)]

## How to fix it

Remove the case from the union: an action nothing can construct is never dispatched. If it must stay, make its `[Obsolete]`
a warning (drop the `true`); the generated override suppresses CS0612/CS0618 and any `DiagnosticId` it names.
