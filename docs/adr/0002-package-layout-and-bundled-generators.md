# ADR-0002: Package layout and bundled generators

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §3, §17.9; owner decision; open question 1; review rounds 4 and 6

## Context

1.x ships three packages, and Ducky.Generator leaks Roslyn dependencies. The owner kept the Ducky/Ducky.Blazor IDs and deprecated Ducky.Generator.

## Decision

Ship Ducky (runtime plus `analyzers/dotnet/cs/Ducky.Generators.dll`), Ducky.Blazor, Ducky.Reactive, Ducky.Draft (independent of Ducky) and Ducky.Testing, all 2.0.0. Generators and analyzers are packed with `IncludeBuildOutput=false` and `PrivateAssets=all`. Ducky.Blazor, Ducky.Reactive and Ducky.Testing reference Ducky with `PrivateAssets="none"` (R-PKG-5, review round 3): the default writes their Ducky dependency as `exclude="Build,Analyzers"`, which would strip the generator and `buildTransitive/Ducky.targets` from an app that installs only Ducky.Blazor. No `InternalsVisibleTo` between shipped assemblies. Ducky.Blazor uses `Microsoft.NET.Sdk.Razor` (still no framework reference), because only the Razor SDK packs `ducky.js` as a static web asset that consuming apps serve; `IsPackable` is false by default and true only in the six shipping projects; Ducky's `buildTransitive` raises DUCKY902 when a 1.x Ducky.Blazor is resolved next to Ducky 2 (its `Ducky >= 1.x` range admits Ducky 2 and it would fail at run time) (review round 4; review round 6 dropped the Ducky.Reactive and Ducky.Testing conditions: 1.x shipped only Ducky, Ducky.Blazor and Ducky.Generator, so no 1.x package of those IDs exists).

## Consequences

One install gives the generator, whichever Ducky package is installed; `PackageSmoke` checks it with a consumer that references only Ducky.Blazor and asserts that no nuspec dependency on Ducky carries an `exclude`, and enforces the dependency rules (R-PKG-1). Generator assets never flow through a `ProjectReference`, so every project that declares actions, slices or draftable records references Ducky (or Ducky.Draft) directly (documented). Ducky.Draft is usable by former Mutty users without adopting Ducky; PackageSmoke proves it with a Draft-only consumer and asserts Draft's zero-dependency nuspec, and it publishes packed consumers to check that `ducky.js` is served (review round 4).

## Alternatives considered

Keep a separate generator package (rejected by the owner); put Blazor features into core (rejected: bigger core for non-Blazor users, see dx-first critique).
