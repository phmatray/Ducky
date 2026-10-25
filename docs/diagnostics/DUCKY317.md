# DUCKY317: PersistStorage.Server with the default KeyPrefix

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A persisted slice uses `PersistStorage.Server`, but `BlazorOptions.KeyPrefix` is still the default `"ducky"`. A
distributed cache is often shared by several apps or environments: two apps with overlapping user ids and the same slice
key would read and overwrite each other's `{prefix}:{scope}:{sliceKey}` entries. The message names the slice.

The check is made on both sides of an Interactive Auto app, because the prefix is shared configuration.

## How to fix it

Set `BlazorOptions.KeyPrefix` in `AddBlazor` to a value unique to the app and environment, such as `"shop-prod"`.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
