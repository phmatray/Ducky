# ADR-0029: Interactivity from RendererInfo, no CircuitHandler

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.4; PER-03; D10; review round 4

## Context

`CircuitHandler` lives in the server-only shared framework and can't be referenced from a WASM-compatible RCL (dx-first's design did not build).

## Decision

An internal `InteractivityGate`: `IsBrowser` → interactive; else the first toucher that knows its renderer (a Ducky component, `DuckyInitializer`, or `StoreSelectionExtensions.Select` with its `RendererInfo` argument) records `RendererInfo.IsInteractive`; if still unknown, a one-time synchronous JS probe (the prerender runtime throws `InvalidOperationException` synchronously from the import call). `PrerenderHandoff` doesn't depend on the gate outside the browser: it always tries the seed and always registers `OnPersisting`, with an explicit `RenderMode.InteractiveAuto` (never `null`: with both render modes configured, the framework rejects a null-mode registration whose target is not a component, and the page's whole state persistence fails; review round 4). The gate is one instance per store, held by the store-owned `PersistenceSlice` and never registered in DI as a scoped service (in WASM that would give the store scope a second gate); it announces the first component registration through a synchronous callback rather than an awaited task, so the browser seed is restored inside the registering component's `Select` without relying on continuation inlining (recovered review round 2). That callback and the `PrerenderSeedWaitTimeout` bound are the only two things that settle the browser seed wait; `RegisterOnRestoring` is not used, since in .NET 10 it fires at registration, not for the initial restore (review round 4).

## Consequences

Ducky.Blazor stays WASM-safe with no framework reference; stores touched first by non-component code still detect prerender.

## Alternatives considered

CircuitHandler (rejected: doesn't build for WASM); `FrameworkReference Microsoft.AspNetCore.App` (rejected: breaks WASM).
