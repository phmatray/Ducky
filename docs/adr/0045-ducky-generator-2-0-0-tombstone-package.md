# ADR-0045: Ducky.Generator 2.0.0 tombstone package

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §3, §19, §21; open question 1; review rounds 4, 5 and 6

## Context

A floating `Ducky.Generator` reference next to Ducky 2.x would double generators and emit duplicate types.

## Decision

Deprecate every Ducky.Generator version on nuget.org and publish one final 2.0.0 package with no assemblies whose `buildTransitive` target raises DUCKY900 with the migration link. It is published exactly once: the project (`Microsoft.Build.NoTargets`, `Version=2.0.0`, `MinVerSkip=true`, `IncludeSymbols=false` and `SuppressDependenciesWhenPacking=true`, so it yields no `.snupkg` and no empty dependency group under warnings as errors, review round 5) packs into `artifacts/tombstone` on every run for PackageSmoke, and `Publish` pushes that directory only for the exact `v2.0.0` tag, never for a prerelease, minor or patch tag (review round 4), with `dotnet nuget push artifacts/tombstone/*.nupkg --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY --skip-duplicate` (review round 6: without `--source` and `--api-key` the push fails after the libraries were pushed, and the tombstone is never published). Its `PackageOutputPath` is anchored at the repository root (`$(RepoRoot)artifacts/tombstone/`), since a relative path resolves against the project directory. A prerelease push would publish a stable Ducky.Generator 2.0.0 before Ducky 2.0 exists; a later push would publish a version the owner's same-day deprecation never covered. Ducky's own `buildTransitive` raises DUCKY900 when any Ducky.Generator is referenced.

## Consequences

Floating references fail loudly instead of silently duplicating code.

## Alternatives considered

Deprecation only (rejected: a floating `*` reference would still resolve 1.x silently).
