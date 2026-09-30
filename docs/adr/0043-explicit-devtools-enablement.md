# ADR-0043: Explicit DevTools enablement

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.8; DT-01; D20

## Context

Auto-enabling through `IHostEnvironment`/`IWebAssemblyHostEnvironment` adds host dependencies to a WASM-safe RCL.

## Decision

`UseDevTools()` enables DevTools when called; apps guard it with their own `IsDevelopment()`. One `Filter` replaces `ShouldLogAction` + `ExcludedActionTypes`.

## Consequences

No host-environment dependency; explicit is visible in Program.cs.

## Alternatives considered

Auto-enable (rejected: dependency); keep two filter options (rejected: same job).
