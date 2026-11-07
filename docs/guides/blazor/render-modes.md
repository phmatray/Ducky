# Render modes and the load rule

`Ducky.Blazor` runs the same store in every Blazor host. What changes between hosts is how long a store lives, which storage it can reach, and whether a prerendered page hands its state over.

```shell
dotnet add package Ducky.Blazor
```

Register it inside `AddDucky` with `AddBlazor()`. `Persist<T>()`, `Prerender<T>()` and `UseDevTools()` call it for you, and so does the generated registration of a slice marked `[Persist]` or `[Prerender]`.

## Components

Inherit `DuckyComponent` (or `DuckyComponent<TState>`, whose `State` is the whole slice, and `DuckyLayout` for layouts), call `Select(...)` in `OnInitialized`, and dispatch from event handlers with `Dispatch(...)`. A store change re-renders the component only when a selected value changed. `Select` after the first render throws (DUCKY352), and DUCKY009 catches a `Select` in markup. `<DuckySelect Selector="...">` renders a fragment from one selection, and `StoreSelectionExtensions.Select` serves a component that must keep another base class. Ducky components work without `AddBlazor()`.

## The render-mode matrix

| Host | Store | Browser storage (Local, Session) | Server storage (`IDistributedCache`) | Prerender seed | Cross-tab | DevTools |
|---|---|---|---|---|---|---|
| WebAssembly standalone | one per app | read at init; each change written on the next turn | not used (Info log once) | none | yes | when `UseDevTools()` |
| Static SSR page | one per request | not used: status `NotStarted` | read at init; written through; the last writes leave with the request scope, after the response ([Static SSR](static-ssr.md)) | none | no | no |
| Prerender pass of an interactive page | one per request, thrown away | not used | as static SSR; its flush can land after the circuit store read or wrote the key | written from the idle store | no | no |
| Interactive Server (circuit) | one per circuit | read at init; written 250 ms after a change; retried after a disconnect; lost only if the circuit ends before a write succeeds | read at init; written through; flushed when the circuit ends | taken at init, before the first render | yes | yes |
| Interactive WebAssembly | one per app | as WebAssembly standalone | not used (restored from the seed only when the slice is also `Prerender<T>`) | taken at init, or at the first Ducky component | yes | yes |
| Interactive Auto | one circuit store and one WebAssembly store, both live in one document after an enhanced navigation | both use the same keys, and each write reaches the other store | circuit side as Interactive Server; WebAssembly side not used | whichever side starts | yes, and between the two stores | yes |

On the server the store is scoped to the circuit or the request. Code that runs in another DI scope (an `IHttpClientFactory` `DelegatingHandler`, a hosted service, a SignalR hub) resolves a different, empty store; reach the circuit's services through a circuit accessor.

In the browser there is one store per app, and its effects resolve scoped services from the store's own scope, so a scoped service injected into an effect (the template's scoped `HttpClient`, for example) is a different instance from the one components get.

A circuit that .NET 10 pauses and resumes is a fresh store: its `Prerender<T>` slices come back from the pause state, its persisted slices from storage, and every other slice starts from its initial state. [Circuit pause and resume](prerender.md#circuit-pause-and-resume) says which one wins for a slice that is both, and when a change not yet written survives the pause.

## One load rule in every mode

Load a slice in an effect on `StoreInitialized`, and return early when the slice was restored. `StoreInitialized` comes after every hydration read the host can perform, so a slice that storage or the prerender seed restored is already in the state:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/RenderModesGuideTests.cs#load-rule-state)]

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/RenderModesGuideTests.cs#load-rule-effect)]

To load the slice once per page rather than once in the prerender pass and again in the interactive store, also mark it `Prerender<T>`, with an `include` predicate that seeds only a loaded slice:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/RenderModesGuideTests.cs#load-rule-register)]

- The rule is the same on Server, WebAssembly and Auto: there is no "wait for `HydrationCompleted`" variant at startup. A user switch during a circuit is different: there a per-user load reacts to `HydrationCompleted` ([After a user switch](persistence.md#after-a-user-switch)).
- Keep the predicate: without it, a load that fails during prerender seeds whatever state the failure left, the slice reads as restored, and the load rule never loads it in the interactive store ([Prerender](prerender.md)).
- Load a `Prerender<T>` slice with an `Effect<T>`, never a reactive effect: the seed waits for running effects, not for reactive pipelines.

Next: [Persistence](persistence.md), [Prerender](prerender.md), [Cross-tab sync](cross-tab.md), [Redux DevTools](devtools.md).
