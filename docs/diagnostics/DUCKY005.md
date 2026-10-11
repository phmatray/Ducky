# DUCKY005: Handler for a non-concrete action type

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build error on the `On<T>` call, or on the name of the
effect (SPEC §16.2).

## What happened

A slice's `On<T>`, an `EffectGroup`'s `On<T>`, or an effect deriving from `Effect<T>` (directly or through a generic
base) names a `T` that is abstract, an interface, `object` or `dynamic` (which is `object` at run time). Reducers and effects match the exact runtime type of an
action (ADR-0008): no action ever has an abstract or interface runtime type, and a handler for `object` would never be
chosen either, so the handler could never run. A type parameter is not reported, since it may be closed with a concrete
type. An effect deriving from an already reported base is not reported again. At run time the same mistake is
DUCKY307.

## How to fix it

Register one handler per concrete action type: `On<ItemAdded>` and `On<ItemRemoved>` rather than `On<IItemAction>`,
and `Effect<ItemAdded>` rather than `Effect<IItemAction>`. Shared logic goes into a private method that each handler
calls.
