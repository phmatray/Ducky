# ADR-0025: Store lifetime: Singleton in the browser, Scoped on the server

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.10, §6.11; SSR-03; D5; INV-22; judge graft; review rounds 4, 6 and 7

## Context

Correctness-first proposed Scoped everywhere. In WASM production, anything resolving `IStore` from the root provider (Program.cs preload) or from an `IHttpClientFactory` handler scope silently gets a second store.

## Decision

Default `Lifetime = OperatingSystem.IsBrowser() ? Singleton : Scoped`. In Singleton mode the store owns one DI scope for effects and middleware, so scoped dependencies resolve legally under Development scope validation. In Scoped mode an internal scoped `StoreDisposeHook`, resolved last at materialization, is disposed first by the container and disposes the store while the scoped dependencies of its effects and middleware are still alive (recovered review round 2). Middleware disposal and the persistence flush wait for the drain and the started inits, never for effect runs, so a hung effect run can't push the flush past the hook's bound and onto disposed services; only effect disposal can land that late (review round 4). Review round 6: nothing is constructed for a store that is disposed before its first use (a server component that injected `IStore` but never selected), no entry point materializes after disposal began, and a materialization in flight is awaited and its instances disposed; each middleware waits only for its own init, and each middleware's `DisposeAsync` is bounded by `DisposeTimeout` (exposed to middleware as `Middleware.DisposeTimeout`, which also bounds the persistence flush), so a hung user middleware can't push the flush or the hook past its bound either. Review round 7: `DisposeAsync` is always bounded; when the drain wait itself expires (one in-flight `Process` step outlasting `DisposeTimeout`), it completes at that bound and the flush and middleware disposal run chained on the drain's exit, after the hook returned, so the late flush catches `ObjectDisposedException` and any provider exception per key and counts the key `ducky.persistence.lost`. A Singleton on a non-browser host warns (EventId 1004, DUCKY004). Spike S-7 validates framework services in WASM.

## Consequences

Exactly one store per browser app (`BrowserLifetime_RootAndScopes_ResolveOneStore`, E2E `Wasm_DelegatingHandlerDispatch_ReachesUiStore`). Scoped effect dependencies in WASM are distinct from the renderer scope's instances (documented).

## Alternatives considered

Scoped everywhere (rejected: silent split store in production); Singleton everywhere (rejected: cross-user leak on Server).
