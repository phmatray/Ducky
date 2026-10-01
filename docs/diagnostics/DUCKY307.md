# DUCKY307: Handler for a non-concrete action type

**Package:** Ducky. **Surface:** `DuckyConfigurationException` (at first store resolution for a slice; wrapped in DUCKY353 for an effect group).

## What happened

A slice or an `EffectGroup` calls `On<T>()` with a `T` that is abstract, an interface or `object`. Handlers match
the exact runtime type of an action, so such a handler could never run. This is the runtime twin of the DUCKY005
analyzer.

## How to fix it

Call `On<T>()` once per concrete action type instead.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
