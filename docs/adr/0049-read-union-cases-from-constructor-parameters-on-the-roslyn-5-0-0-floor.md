# ADR-0049: Read union cases from constructor parameters on the Roslyn 5.0.0 floor

- Status: Proposed
- Date: 2026-11-17
- Related: ADR-0003; spike S-7 (union) in `docs/spec/spikes.md`; #311, #312

## Context

The union-first API (#311) needs the generator to register one handler per union case. The generators are built against
`Microsoft.CodeAnalysis.CSharp` 5.0.0, which predates C# 15. The question was whether such a generator, running in the
SDK 11 compiler, can see a source-declared union's cases. Options: (a) read cases through the 5.0.0 API surface, (b) raise
the generator's Roslyn floor to SDK 11's (superseding the generator half of ADR-0003), (c) cases declared by an attribute
the author writes.

## Decision

(a). Detect a union by `System.Runtime.CompilerServices.IUnion` in `INamedTypeSymbol.AllInterfaces`, and read its cases as
the parameter types of its public, single-parameter instance constructors. Keep the 5.0.0 floor and its Renovate ignore.

## Evidence

Spike S-7 (union): with the 5.0.0 generator on SDK 11.0.100, a source-declared `union CartAction(AddItem, RemoveItem,
Clear);` and a union from a referenced assembly both present as a sealed struct implementing `IUnion` with one public
constructor per case. The `[Union]` attribute exists only on the referenced one, so it is not used for detection.

## Consequences

No generator-floor change, so SDK 10-era consumers of non-union code are unaffected (the net11.0-only retarget, #302,
decides the rest). The convention is an observed compiler behaviour of 11.0.100, not a documented contract: the
generator's tests must pin it, and a union with a user-declared constructor or generic cases is unmeasured. Fall back to
(b) only if a later compiler stops lowering unions to single-parameter constructors.

## Alternatives considered

(b) Rejected for now: needless while (a) works; it would make consumers on older SDKs fail with CS9057 (S-1).
(c) Rejected: duplicates what the `union` declaration already states and can drift from it.
