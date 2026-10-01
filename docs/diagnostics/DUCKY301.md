# DUCKY301: Transient store lifetime

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

`DuckyBuilder.Lifetime` was set to `ServiceLifetime.Transient`. A transient store would give every resolution its
own store, so components, effects and services would each see different state.

## How to fix it

Set `Lifetime` to `ServiceLifetime.Singleton` or `ServiceLifetime.Scoped`, or leave it unset: the default is
`Singleton` in the browser and `Scoped` on the server (one store per circuit or request).

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
