# Cross-tab sync

A slice persisted in `localStorage` can follow the changes the user makes in their other tabs:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/CrossTabGuideTests.cs#cross-tab-register)]

`[Persist(SyncAcrossTabs = true)]` does the same. Only `localStorage` is shared between tabs: syncing a Session slice is a configuration error (DUCKY312).

## What another tab sees

- A change written in one tab reaches the others through the browser's `storage` event. The receiving store waits 50 ms, reads the entry's current value and restores it, so the newest write wins whatever order the events arrive in.
- A restore from another tab is never written back, so two tabs don't echo each other's writes.
- `ClearPersistedState` in one tab removes the entries; the other tabs reset their synced slices to the initial state and keep their own state otherwise.
- With a [`Scope`](persistence.md), a tab accepts only its own scope's entries: a tab still signed in as another user writes other keys, and they are ignored.
- A circuit that missed notifications while disconnected re-reads the affected entries once it reconnects, in place of the events it missed.
- Like every restore, a value from another tab replaces the whole slice.

## One page, two stores

In an Interactive Auto app, the circuit store and the WebAssembly store can both be live in one page after an enhanced navigation. They share `localStorage` and `sessionStorage`, so every write and removal of a Local or Session entry also reaches the other store of the page, synced or not. Without it the two stores would overwrite each other's entries.
