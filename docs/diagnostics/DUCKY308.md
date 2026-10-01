# DUCKY308: Duplicate handler for one action type

**Package:** Ducky. **Surface:** `DuckyConfigurationException` (at first store resolution for a slice; wrapped in DUCKY353 for an effect group).

## What happened

A slice or an `EffectGroup` calls `On<T>()` more than once for the same `T`. Only one handler per exact action type
is allowed. This is the runtime twin of the DUCKY014 analyzer.

## How to fix it

Keep one `On<T>()` call for that action type and merge the handlers into it.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
