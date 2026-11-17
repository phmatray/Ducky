# DUCKY028: MethodName ignored on a union [DuckyAction]

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build warning on the union's name (SPEC §16.1, GEN-01).

## What happened

`[DuckyAction]` on a C# union generates the helpers of each of its cases (ADR-0050), not of the union itself: dispatch
unwraps a union to its case (#304), so `store.AddItem("apple")` and `store.Dispatch(new CartAction(new AddItem("apple")))`
deliver the same action. Each case's helpers are named after the case, so one `MethodName` can't name them all, and the
one set on this union is ignored.

For example, this code is reported:

[!code-csharp[](../../test/Ducky.Generators.Tests/Docs/DiagnosticExamplesTests.cs#DUCKY028)]

## How to fix it

Remove `MethodName` from the union. To name one case's helpers, put `[DuckyAction(MethodName = "...")]` on that case: a
case carrying its own `[DuckyAction]` keeps its own helpers, and the union generates none for it.
