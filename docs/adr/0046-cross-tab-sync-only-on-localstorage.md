# ADR-0046: Cross-tab sync only on localStorage

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §2.2 D17, §8.2 DUCKY312, §11.7; XT-01; INV-18; review round 3

## Context

The matrix allowed `SyncAcrossTabs` on any storage. `sessionStorage` is per tab and fires no cross-tab `storage` event; a Server-cache cross-circuit channel would be SSR-05 by another name (rejected: cross-user leaks).

## Decision

`SyncAcrossTabs` is valid only with `PersistStorage.Local`; anything else is the configuration error DUCKY312. Within one document (Interactive Auto), `ducky.js` relays each write **and removal** of **every** browser-persisted key, Local or Session, synced or not, to the document's other stores, since `storage` events never fire in the writing document and both stores share one localStorage and one sessionStorage. Every store with a Local or Session slice registers for the relay; only stores with a `Local` + `SyncAcrossTabs` slice also get the cross-tab `storage` listener. A notification that fails while a circuit is disconnected is followed by a full re-read, and the module prunes registrations whose .NET object is gone.

## Consequences

One transport, one test matrix. The circuit store and the WASM store of one document converge on every browser-persisted slice (`Auto_SessionAndUnsyncedLocalSlices_ConvergeInOneDocument`, `Auto_ClearInCircuitStore_ResetsWasmStore`). Server-storage circuits of one user don't sync and are last-writer-wins (documented residual risk).

## Alternatives considered

Session storage "sync" (rejected: no event exists); a server hub (rejected: SSR-05).
