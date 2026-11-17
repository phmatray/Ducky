# ADR-0049: net11.0 only, Roslyn floor of SDK 10.0.100

- Status: Accepted
- Date: 2026-11-17
- Related: SPEC §3 R-PKG-3; #300, #302; supersedes ADR-0003; owner decision 2026-11-17

## Context

ADR-0003 chose net10.0 only. Three facts changed since:

- The dotnet-monorepo 11.x packages (`Microsoft.AspNetCore.Components.*`, `Microsoft.Extensions.*`, renovate #293) ship only `lib/net11.0`.
- C# 15 union types, the feature Ducky 2.x wants to build on, exist only on net11.0 (measured on SDK 11.0.100: `union` compiles with the default LangVersion on net11.0 and is not a keyword on net10.0).
- .NET 10 (LTS) and .NET 11 (STS) both reach end of support on 2028-11-14, so dropping net10.0 shortens no consumer's support window. Ducky 2.0 is still a release candidate, so no 2.x user is stranded.

## Decision

Every Ducky 2.x library, test, sample and benchmark targets net11.0 only (owner decision). Packages ship `lib/net11.0/` and no `lib/net10.0/`.

The generator half of ADR-0003 is unchanged: generators stay netstandard2.0 on `Microsoft.CodeAnalysis.CSharp` 5.0.0, the Roslyn of SDK 10.0.100, which SDK 11 still loads. The Renovate ignore rule on it stays.

The verbatim 1.x legacy fixtures under `docs/migration/samples/legacy/` keep net10.0: they model the upstream third-party libraries as they were published.

## Consequences

- A net10.0 app cannot reference Ducky 2.x; it needs SDK 11 and net11.0.
- One test, coverage, mutation and PackageSmoke run; no `#if` API divergence.
- Unions can appear in Ducky's own public API (separate issues).
- The dotnet monorepo is unpinned from the 10.0 line.

## Alternatives considered

Multi-target net10.0 + net11.0 (rejected: doubles the matrix, gains no support window, and unions need net11.0).
