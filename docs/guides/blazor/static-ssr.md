# Static SSR with Server storage

A static SSR page gets one store per request, and its HTML is the only output: no circuit follows to correct it. Slices persisted with `PersistStorage.Server` are read from `IDistributedCache` during init. Two rules follow from when that read and the matching write happen.

## Reading: await init in a lifecycle method

The stored value reaches the HTML only when a lifecycle method awaits `IStore.InitializeAsync`, because the static SSR quiescence wait covers component lifecycle tasks and nothing else.

1. Wrap the page content in `<DuckyInitializer>`, which awaits `InitializeAsync` in `OnInitializedAsync` and renders its `ChildContent` afterwards. Awaiting `Store.InitializeAsync()` in the component's own `OnInitializedAsync` works too.
2. Don't rely on a component that only calls `Select` in `OnInitialized`. It starts init but leaves no lifecycle task pending, so its HTML shows the initial state.
3. Test against an asynchronous cache. `MemoryDistributedCache` completes `GetAsync` synchronously, so the restore runs before the render and hides the difference; Redis renders the initial state.

The prerender pass of an interactive page follows the same rule for its HTML. Its seed carries the stored values in any case, because `OnPersisting` waits for idle.

## Writing: Post/Redirect/Get can show the value from before the post

A form handler's write leaves with the request-scope flush, which runs after the redirect response was sent. The browser's immediate GET builds a new store that can read the old value, and a later request sees the new one.

Don't count on the redirect target showing what the post just wrote. Ducky 2.0 has no public flush API. The 2.1 upgrade path is a `FlushPersistenceAsync` over the same flush, awaited before `NavigateTo`.
