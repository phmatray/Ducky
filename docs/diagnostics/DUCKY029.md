# DUCKY029: Union case can never be dispatched

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the slice's or effect group's name (ADR-0051).

## What happened

The generator registers each case of a `Slice<TState, TAction>` or an `EffectGroup<TAction>` with `On<Case>`, and the
store matches a handler by the exact runtime type of the dispatched action (ADR-0008). A case that is abstract, an
interface or `object` is never the runtime type of an action, so `On<Case>` would throw DUCKY307 when the slice is built.
A `Nullable<T>` case (`int?`) is never one either: a boxed `T?` is a `T`, so its handler would never run. The message
names those cases, and no case of the type is registered.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY029)]

## How to fix it

Make each case a concrete type the store can dispatch: list the concrete action records in the union instead of their
interface or base record, and `T` instead of `T?`.
