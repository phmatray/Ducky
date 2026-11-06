# DUCKY005: Handler for a non-concrete action type

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build error on the `On<T>` or `OfActionType<T>` call, or
on the name of the effect (SPEC §16.2).

## What happened

A slice's `On<T>`, an `EffectGroup`'s `On<T>`, an effect deriving from `Effect<T>` (directly or through a generic
base), or Ducky.Reactive's `OfActionType<T>` filter names a `T` that is abstract, an interface, `object` or `dynamic`
(which is `object` at run time). Reducers, effects and `OfActionType<T>` match the exact runtime type of an action
(ADR-0008): no action ever has an abstract or interface runtime type, and a handler for `object` would never be chosen
either, so the handler could never run and the filter would never let an action through. A type parameter is not
reported, since it may be closed with a concrete type. An effect deriving from an already reported base is not reported
again. At run time the same mistake in `On<T>` is DUCKY307.

## How to fix it

Register one handler per concrete action type: `On<ItemAdded>` and `On<ItemRemoved>` rather than `On<IItemAction>`,
`Effect<ItemAdded>` rather than `Effect<IItemAction>`, and merge `actions.OfActionType<ItemAdded>()` with
`actions.OfActionType<ItemRemoved>()` rather than filtering on `IItemAction`. Shared logic goes into a private method
that each handler calls.
