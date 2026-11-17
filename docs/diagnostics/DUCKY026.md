# DUCKY026: Union slice action type is not a union

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the slice's name (ADR-0051).

## What happened

A slice deriving from `Slice<TState, TAction>` reduces the cases of the C# union `TAction` through one `Reduce`, and the
generator registers each case for it. This slice's `TAction` is not a union (a C# 15 `union` declaration, which implements
`System.Runtime.CompilerServices.IUnion`), so there are no cases to register and the slice would handle nothing.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY026)]

## How to fix it

Declare the slice's actions as a union, `public union CartAction(AddItem, RemoveItem);`, and derive from
`Slice<Cart, CartAction>`. For a single action type, derive from `Slice<TState>` and register it with `On<TAction>`.
