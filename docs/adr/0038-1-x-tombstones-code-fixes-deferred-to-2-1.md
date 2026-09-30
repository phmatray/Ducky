# ADR-0038: 1.x tombstones; code fixes deferred to 2.1

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §22.1, §16.1; D22; review rounds 3 and 5

## Context

Removing 1.x types produces cascades of CS0246. dx-first's single analyzer DLL with code fixes trips RS1038.

## Decision

Ship member-less error-level `[Obsolete(DiagnosticId = "DUCKYM00n")]` tombstones, declared as interfaces even for 1.x classes (review round 3: an interface has no constructor to cover, and test code can't reference an error-level obsolete type), for the 1.x types user code derives from, taken from the published 1.0.292 public API (review round 5: the first table came from the unreleased `dev` branch, so it tombstoned types no released consumer uses, put `AsyncEffect<TAction>` in the wrong namespace and missed published base types such as the `Fsa` records, `NormalizedState`, `ISlice<TState>` and the generic `DuckyLayout<TState>`), in their published namespaces, only where no 2.0 name clashes and no name users import alongside clashes (so no `IMiddleware` tombstone: it would be ambiguous with `Microsoft.AspNetCore.Http.IMiddleware` under Web SDK implicit usings). The 1.0.292 public type list is committed as `docs/migration/ducky-1x-api.txt`, and the `Docs` target fails when a listed type has neither a tombstone nor a mapping row in the guide; members (`DuckyComponent.Dispatcher`, `GetSliceState<T>`, `OnAfterSubscribed`, the 1.x `AddDucky` overload) are covered by mapping rows. No code fixes in 2.0; in 2.1 they ship as a separate `Ducky.CodeFixes.dll`.

## Consequences

One precise error per usage with a link; no coverage cost (spike S-2 verifies it); smaller 2.0 surface.

## Alternatives considered

No tombstones (rejected: migration cost); code fixes in the analyzer DLL (rejected: RS1038).
