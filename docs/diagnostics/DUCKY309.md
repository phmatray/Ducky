# DUCKY309: Unresolvable constructor dependency

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

An effect, middleware or reactive effect has a constructor parameter that no candidate constructor can resolve from the
service provider. A `[FromKeyedServices(key)]` parameter is checked against keyed registrations; a `[ServiceKey]`
parameter never resolves, because Ducky never creates keyed effects or middleware. The message names the type, what
registered it, the missing service and its key.

## How to fix it

Register the missing service (as a keyed service with that key when the parameter is keyed) in the
`IServiceCollection`, or give that parameter a default value.

For a `[ServiceKey]` parameter no registration helps: remove `[ServiceKey]` from it, or give it a default value.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
