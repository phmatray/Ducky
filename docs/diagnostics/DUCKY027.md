# DUCKY027: Union-first type is not partial

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the name of the slice, effect group or union effect (ADR-0051, ADR-0052).

## What happened

The generator registers the cases of a `Slice<TState, TAction>`, an `EffectGroup<TAction>` or an `Effect<TAction>` with a
union `TAction` by overriding `RegisterCases` in the type's own declaration, which it can only extend when the type, and
every type it is nested in, is `partial`. This type or one of its containing types is not, so no case is registered and
the type would handle nothing. A non-union `Effect<TAction>` is an ordinary effect that the union rules do not apply to.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY027)]

## How to fix it

Add `partial` to the types the message names: `public sealed partial class CartSlice : Slice<Cart, CartAction>`,
`public sealed partial class CartEffects : EffectGroup<CartAction>`, or
`public sealed partial class LoadCartEffect : Effect<CartAction>`.
