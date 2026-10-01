# ADR-0030: Large interop payloads through IJSStreamReference

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.5, §11.7, §11.8, §11.9; D11; INV-18, INV-23; review rounds 4 and 6

## Context

On Blazor Server any JS→.NET message above `MaximumReceiveMessageSize` (32 KB) closes the circuit; another tab can trigger it via a storage event.

## Decision

Values up to `InlinePayloadBytes` (16 KiB) cross as strings; larger reads, cross-tab values and DevTools imports use `IJSStreamReference` with `maxAllowedSize: MaxPayloadBytes`, all through one pull pattern: `ducky.js` keeps the value, notifies .NET with a too-large flag, and .NET takes it through an export (`storageGetStream`, `devtoolsTakeMessage`). DevTools jumps cross inline because `ducky.js` strips the extension's `state` from them (history is typed on the .NET side) (review round 4). When nothing is kept, a pull export returns a one-byte `Uint8Array`, never an empty one or `null`: for `InvokeAsync<IJSStreamReference>` Blazor wraps the result with `createJSStreamReference`, which throws on `null`, and spike S-5 measured that an empty array throws too, so .NET treats `Length <= 1` as not found (an envelope or a DevTools message is at least 2 bytes); every `IJSStreamReference` is read under `await using`, so it never leaves its buffer pinned in the circuit's JS object table (review round 6).

## Consequences

No circuit kill from large state; both branches are unit-testable with a fake stream reference, and a real-runtime E2E (`CrossTab_TooLargeKeyRemovedBeforePull_RealRuntime_ResetsSlice`) checks the one-byte contract that a fake runtime can't.

## Alternatives considered

Raise `MaximumReceiveMessageSize` in docs (rejected: global and fragile).
