# DUCKY008: Store injected into an effect or middleware constructor

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the constructor parameter (SPEC §6.6,
§16.2).

## What happened

An `Effect<T>`, `EffectGroup` or `Middleware` constructor takes an `IStore` or `IDispatcher` parameter, primary
constructors included. The store creates its effects and middleware on its first use, while it is materializing: a
constructor that uses the store re-enters materialization and throws `InvalidOperationException`, and an effect that
dispatches through an injected store bypasses the supersession and disposal rules of its run.

## How to fix it

In an effect, dispatch through the `EffectContext` the handler receives (`ctx.Dispatch`, `ctx.DispatchAsync`), and read
state through it. In a middleware, use the `Store` property, which is attached before any hook runs. Slices and
`SliceStore<TState>` may still be injected.
