# DUCKY027: Union slice is not partial

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the slice's name (ADR-0051).

## What happened

The generator registers the cases of a `Slice<TState, TAction>` by overriding `RegisterCases` in the slice's own
declaration, which it can only extend when the slice, and every type it is nested in, is `partial`. This slice or one of
its containing types is not, so no case is registered and the slice would handle nothing.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY027)]

## How to fix it

Add `partial` to the types the message names: `public sealed partial class CartSlice : Slice<Cart, CartAction>`.
