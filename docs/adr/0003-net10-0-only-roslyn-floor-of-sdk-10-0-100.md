# ADR-0003: net10.0 only, Roslyn floor of SDK 10.0.100

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §3 R-PKG-3; matrix conflict 18; open question 13

## Context

The matrix asked net10.0 vs net8.0+net10.0 and which Roslyn floor to target. Attributes now live in the runtime assembly, so the generator no longer needs a recent Roslyn API.

## Decision

Target net10.0 only (owner decision). Build generators against the `Microsoft.CodeAnalysis.CSharp` version shipped with SDK 10.0.100 (spike S-1), pinned with a Renovate ignore rule.

## Consequences

`System.Threading.Lock`, `DiagnosticMethodInfo`, `RendererInfo`, frozen collections and `TimeProvider` are all available. A consumer always has SDK ≥ 10.0.100, so a lower floor gains nothing.

## Alternatives considered

Multi-target net8.0 (rejected: owner decision, doubles the test matrix, and several required APIs are .NET 9+).
