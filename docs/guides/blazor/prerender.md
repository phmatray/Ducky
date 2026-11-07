# Prerender

An interactive page is rendered twice: once on the server as HTML (the prerender pass, with its own throwaway store), then by the interactive store in the circuit or in WebAssembly. A `Prerender<T>` slice carries its prerendered state across, so the interactive store starts from it: no flicker, and no second load.

## Seed a slice

Given a forecast loaded at startup:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PrerenderGuideTests.cs#prerender-state)]

load it with the [load rule](render-modes.md), in an `Effect<T>`:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PrerenderGuideTests.cs#prerender-effect)]

and register it as prerendered:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/PrerenderGuideTests.cs#prerender-register)]

When the prerender pass ends, Ducky waits until its store is idle (up to `PrerenderIdleTimeout`, 5 seconds) and writes the seed into the page's persisted component state. If the store is still busy at `PrerenderIdleTimeout`, no seed is written (a Warning is logged) and the interactive store loads every slice itself. The interactive store takes it when it starts, before the first render, and the slice reads as restored (`WasRestored`), so the load effect returns early. `[Prerender]` on the slice is the same as `Prerender<T>()`.

## Leave failed loads out

Idle only means no effect is running, not that the load succeeded. A load can fail during prerender (a server-side `HttpClient` calling a relative URL is the classic case); seeded, the error state would read as restored and the browser would never retry. The `include` predicate above seeds the slice only when it holds a loaded forecast; otherwise the interactive store loads it again.

## Load it with an `Effect<T>`

The idle wait counts running effects. It doesn't count `LongRunning` effects, so a poller never holds the seed back, and it doesn't count Ducky.Reactive pipelines at all: a slice loaded by a reactive effect would be seeded in its loading state, and the interactive side would take that as loaded. Load every `Prerender<T>` slice with an `Effect<T>`.

## What the seed can hold

- **It is public.** The WebAssembly copy of the seed is in the page HTML, like the rendered markup: a `Prerender<T>` slice must hold nothing the rendered page couldn't show.
- **It has a budget.** On Interactive Server the seed travels back in the message that starts the circuit, which Blazor caps at 32 KB for everything the page persists. A slice that would take the seed past `PrerenderSeedMaxWireBytes` (20 KB of wire size, about 8 KB of JSON state) is left out with a Warning, and the interactive side loads it again: slower, never wrong. The remaining room is for other persisted component state, such as the template's authentication state.
- **Storage wins.** A slice that is also persisted takes the seed first, then the value in storage when there is a valid entry (one extra render). There is no timestamp comparison, since client and server clocks differ: a valid entry wins even when it is older than the seed.

## WebAssembly

WebAssembly restores the page's state only when the host runs, so the store takes the seed at init, or at the first Ducky component (or `<DuckyInitializer>`) if init started earlier. Don't await a store preload in `Program.cs` before `RunAsync`: init then waits at most `PrerenderSeedWaitTimeout` (250 ms) for the seed, and logs a Warning that names this fix.

## Circuit pause and resume

When .NET 10 pauses a circuit, the same mechanism writes the circuit's `Prerender<T>` slices into the pause state, and the resumed circuit takes them back. A slice that is both `Prerender<T>` and persisted in browser storage (Local/Session) keeps a change the circuit couldn't write before the pause: the pause state carries it and it is written again. A scoped slice keeps it only if the same user resumes. Every other persisted slice comes back from storage. A browser-storage change the circuit hadn't written before the pause is lost. A Server-storage change is not, because the paused circuit's store flushes its pending writes when it ends (unless the cache is still failing, see [Ceilings](persistence.md#ceilings)), so the resumed circuit reads it from the cache. Every other slice starts from its initial state.
