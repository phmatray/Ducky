# DUCKY351: SliceStore used before it was attached

**Package:** Ducky. **Surface:** `DuckyConfigurationException`, thrown synchronously by `State`, `Set` or `SetAsync`.

## What happened

A `SliceStore` was read or written before a store attached it, which means it was created with `new` instead of
being resolved from dependency injection.

## How to fix it

Inject the `SliceStore` (`AddSlice<TSlice>()` registers it as a service resolving to the store-owned instance);
don't create it with `new`.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
