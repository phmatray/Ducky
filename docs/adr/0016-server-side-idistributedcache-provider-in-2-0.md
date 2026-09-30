# ADR-0016: Server-side IDistributedCache provider in 2.0

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.6; PER-04; matrix conflict 13; open question 9; review rounds 1 and 7

## Context

On Blazor Server, scoped services are disposed after the circuit is gone, so browser-storage flush-on-dispose can't run: the debounce window is a loss window.

## Decision

Ship `PersistStorage.Server` backed by the app's `IDistributedCache`. The key always contains a non-null scope; the default scope is the authenticated `NameIdentifier`, anonymous means no I/O. The scope is re-resolved on `AuthenticationStateChanged` in every host (in the browser through the renderer-scope provider), and the switch resets scoped slices synchronously in one restore and derives each write's key from the snapshot's scope epoch, so no write can cross scopes. A same-user notification whose scope resolves synchronously while the current attempt has finished (every Blazor Server circuit reconnect) is a re-assertion, not a switch: nothing is reset or re-read (review round 7). `KeyPrefix` must be app-unique when Server storage is used (DUCKY317), because caches are shared between apps. In the browser Server-storage slices are inert rather than an error, so Interactive Auto registrations start on both sides. `IPersistenceProvider` stays internal; storage is chosen by enum.

## Consequences

Closes the Server loss window; works in prerender and static SSR, where the stored values reach the rendered HTML only when a lifecycle method awaits `InitializeAsync` (`<DuckyInitializer>`, which awaits it in `OnInitializedAsync`; the prerender seed carries them in any case, review round 7); leak-safe by construction (INV-17). Concurrent circuits of one user are last-writer-wins on whole slices (documented residual risk).

## Alternatives considered

Document the loss window only (rejected: owner promoted PER-04); mandatory explicit scope resolver (rejected: the leak-safe default is simpler); public provider interface (deferred to 2.1 with IndexedDB).
