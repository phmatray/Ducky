# DUCKY032: Union case is obsolete as an error

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the name of the slice, effect group or union effect (ADR-0051, ADR-0052).

## What happened

The generator writes the `RegisterCases` override of every `Slice<TState, TAction>` and `EffectGroup<TAction>`, one
`On<Case>` per case of the union, and of every `Effect<TAction>` with a union `TAction`, one `OnCase<Case>` per case. The
override names each case, the unions that wrap it and the constructors those wraps call. One
of them carries `[Obsolete(…, true)]`, or sits in a type that does, and CS0619 can't be suppressed, so the override would not
compile. The generator writes nothing for this type, and reports this instead, naming what is obsolete. A union declared in
an obsolete context may list such a case; the generated override is in that context only when the slice, effect group or
union effect, or a type containing it, is itself `[Obsolete]`, and then it is generated as usual.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY032)]

## How to fix it

Remove the case from the union: an action nothing can construct is never dispatched. If it must stay, make its `[Obsolete]`
a warning (drop the `true`); the generated override suppresses CS0612/CS0618 and any `DiagnosticId` it names.
