# ADR-0020: JS modules held to 100% coverage through Playwright

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.9, §17.2; matrix conflict 17; open question 14

## Context

100% is achievable for C#; the JS interop module is the remaining untested surface.

## Decision

`ducky.js` is covered by Microsoft.Playwright for .NET, one toolchain with the rest of the tests. A CDP precise-coverage helper fails on any zero-count block (100% block coverage). Chromium is required on PRs; Firefox and WebKit run nightly (functional).

## Consequences

No second (Node) toolchain; the JS bar equals the C# bar.

## Alternatives considered

Node `@playwright/test` + monocart (rejected: second toolchain, more required-CI surface); report-only JS coverage (rejected: bar).
