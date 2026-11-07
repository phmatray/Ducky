# Persistence

A persisted slice is written to storage when it changes and restored when the next store starts: from `localStorage` or `sessionStorage` in the browser, or from the server's `IDistributedCache`.

## Browser storage

A persisted slice pins its `Key`, which names its storage entry:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-state)]

Persistence serializes through the JSON context passed to `UseJson`, so list each persisted state type in it (DUCKY006 lists the ones missing, DUCKY306 fails at startup for a persisted one):

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-json)]

Then register the slice as persisted:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-browser)]

`[Persist]` on the slice does the same through the generated registration, with `Storage`, `Version` and `SyncAcrossTabs` as attribute properties (`[Persist(Storage = PersistStorage.Session)]`). Migrations, `MaxAge` and `Debounce` take a `Persist<T>(...)` call, whose values win over the attribute's. The entry is `{KeyPrefix}:{Key}` (`ducky:preferences` here) and holds `{"v":1,"at":"…","s":<state>}`. `PersistStorage.Session` keeps it per tab. Ducky 1.x entries don't parse and are discarded.

The slice reads as restored once a valid entry is restored: `State.WasRestored<Preferences>()` is true, which is what the [load rule](render-modes.md) checks.

## Versions and expiry

Change the stored shape with a version and a migration step per version:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-version)]

- An entry with an older version runs every step up to `Version`, over `JsonNode`, before it is deserialized.
- An entry with a newer version than the app's (an older deployment reading what a newer one wrote) is not restored, with a Warning.
- An entry older than `MaxAge` is discarded. On Server storage, `MaxAge` also sets the cache entry's expiry.
- An entry that fails to deserialize leaves its slice at its initial state, with a Warning; the other slices still restore.

## Waiting for hydration

The `@ducky/persistence` slice tells where hydration is: `NotStarted` (a prerender or static SSR store with only browser slices), `Hydrating`, `Hydrated` or `Failed`. Select it to show a loading state:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-status)]

Hydration ends within `HydrationTimeout` (5 seconds, below the store's `InitTimeout`, DUCKY316). On a timeout or a read failure the status is `Failed`, the slices keep their initial state, and the first change to such a slice overwrites what storage held.

## Server storage

`PersistStorage.Server` keeps the slice in the app's `IDistributedCache` (Redis, SQL Server, memory), per user:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-server)]

- The entry is `{KeyPrefix}:{user}:{Key}`, the user being the `ClaimTypes.NameIdentifier` claim from the `AuthenticationStateProvider` (DUCKY315 if none is registered, DUCKY314 without an `IDistributedCache`).
- Set an app-unique `KeyPrefix` (DUCKY317 keeps the default from being used): caches are often shared between apps and environments.
- An anonymous user has no key: nothing is read or written for them, and there is never a fallback to a shared key.
- Every change is written at once and retried until it succeeds or the store ends; the last writes are flushed when the circuit or request ends.
- Two circuits of one user (two tabs) write the same entry, last writer wins on the whole slice. A circuit reads only when it starts.
- A sign-out, or another user signing in, during a circuit moves the store to the new user's entries: the scoped slices reset to their initial state, then the new user's entries are read. See [After a user switch](#after-a-user-switch) for what that means for loads.
- In the browser (WebAssembly, the WebAssembly side of Auto) Server-storage slices are not persisted, so one shared registration works on both sides.

A static SSR page shows stored values only when a lifecycle method awaits the store's initialization; see [Static SSR with Server storage](static-ssr.md).

## Scopes

`BlazorOptions.Scope` replaces the user segment, for example with a tenant:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-scope)]

Once set, the scope applies to browser keys as well (`{KeyPrefix}:{scope}:{Key}`), and a null scope means no reads and no writes for them too. The scope must be unique across the identity providers the app accepts. In a WebAssembly app with a `Scope` and no Ducky component, render `<DuckyInitializer>` so the store receives the page's services; otherwise scoped hydration ends in `Failed`.

### After a user switch

When the scope changes during a circuit (sign-out, another user signing in, a new tenant), the scoped slices go through a new hydration attempt:

- A change made to a scoped slice during the switch is not persisted, and the new user's stored value replaces it.
- The reset is a hydration restore, so `WasRestored` is true for the scoped slices afterwards. The [load rule](render-modes.md#one-load-rule-in-every-mode) on `StoreInitialized` does not run again, and checking `WasRestored` would skip the load anyway. To load the new user's data, react to the new attempt's `HydrationCompleted` instead.
- A circuit reconnect re-sends the same user. That resets nothing as long as the `Scope` delegate completes synchronously, as `DuckyScopes.NameIdentifier` does. A delegate that completes asynchronously makes every reconnect reset the scoped slices to their initial state for one render, until the re-read restores them. Keep it synchronous-completing.

## Clearing on sign-out

`ClearPersistedState` removes this store's entries of every persisted slice from storage. It leaves the store's state as it is, so reset the slices that hold the user's data yourself, before the clear:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PersistenceGuideTests.cs#persistence-clear)]

The other store of an Interactive Auto page receives the removal of the Local and Session entries and resets those slices to their initial state. Other tabs do so only for Local slices with `SyncAcrossTabs`. Server-storage slices are not synced: another circuit of the same user keeps its in-memory state until it restarts.

## Ceilings

| | Default | What happens past it |
|---|---|---|
| `HydrationTimeout` | 5 s | status `Failed`, slices keep their initial state |
| `InlinePayloadBytes` | 16 KB | a value read from the browser crosses as a stream (Blazor Server's hub message limit is 32 KB) |
| `MaxPayloadBytes` | 5 MB | a stored value that large reads as not found, with a Warning; the next write replaces it |
| `Debounce` | 0; 250 ms for browser storage on Interactive Server | changes inside the window are written once |

Where a change can still be lost:

- **WebAssembly:** each change is written on the next scheduler turn; a page unload in between loses it.
- **Interactive Server with browser storage:** a write that fails while the circuit is disconnected is retried (1 s doubling to 30 s), but a circuit that ends before a write succeeds loses it (counted as `ducky.persistence.lost`). Use `PersistStorage.Server` to close that window.
- **Server storage:** a process crash during a write or a pending retry, or a cache still failing when the store ends (counted as `ducky.persistence.lost`).
- **Server storage and prerendering:** the prerender pass's writes, and its flush when the request ends, can land after the interactive circuit already read or wrote the same key, overwriting the newer state. With per-page interactivity, a new circuit can likewise read before the previous circuit's last write lands. Don't change Server-storage slices during prerender.
