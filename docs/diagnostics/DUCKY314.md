# DUCKY314: PersistStorage.Server without an IDistributedCache

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A persisted slice uses `PersistStorage.Server`, but the app registers no `IDistributedCache`, which Server storage reads
and writes. The message names the slice.

The check is not made in the browser (`OperatingSystem.IsBrowser()`): Server storage is inert there, so the shared
Interactive Auto registration starts on the WebAssembly side without a cache.

## How to fix it

Register a distributed cache (`AddStackExchangeRedisCache`, `AddDistributedSqlServerCache`, or
`AddDistributedMemoryCache` for a single server), or set `Storage = PersistStorage.Local` in `Persist<TSlice>`.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
