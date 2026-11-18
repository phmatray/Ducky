# DUCKY319: Handler for a union action type

**Package:** Ducky. **Surface:** `DuckyConfigurationException` (at first store resolution for a slice; wrapped in DUCKY353 for an effect group).

## What happened

A slice or an `EffectGroup` calls `On<T>()`, or a union `Effect<TAction>` calls `OnCase<T>()`, with a C# 15 `union` type (a type implementing
`System.Runtime.CompilerServices.IUnion`). Dispatch unwraps a union action to its case before anything else sees it, so
reducers and effects only ever receive the case types, and a handler registered on the union itself could never run.

## How to fix it

Call `On<T>()` once per case type of the union instead. Dispatching the union value then runs the handler of its case.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page.
