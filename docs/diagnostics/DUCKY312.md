# DUCKY312: SyncAcrossTabs without local storage

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A persisted slice sets `PersistOptions.SyncAcrossTabs` while its `Storage` is `PersistStorage.Session` or
`PersistStorage.Server`. Tabs are kept in sync through `localStorage` only: session storage belongs to one tab, and
server storage has no browser event to follow. The message names the slice and its storage.

## How to fix it

Set `Storage = PersistStorage.Local` in `Persist<TSlice>`, or turn `SyncAcrossTabs` off.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
