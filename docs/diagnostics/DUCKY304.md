# DUCKY304: Duplicate slice key

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

Two registered slices have the same key. The message names both slice types and the key. Keys identify slices in the
snapshot, in persistence and in DevTools, so they must be unique per store.

## How to fix it

Override `Key` in one of the two slices to return a key no other slice uses.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
