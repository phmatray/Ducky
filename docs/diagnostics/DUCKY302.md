# DUCKY302: Generic slice without an explicit key

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A slice registered with `AddSlice<TSlice>()` is a generic type and does not override `Key`. A slice key is derived
from the type name, and every closed type of a generic slice would derive the same key.

## How to fix it

Override `Key` in the generic slice so that each closed generic type returns its own fixed key, or derive a
non-generic slice from it.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
