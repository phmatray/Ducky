# DUCKY900: Ducky.Generator referenced next to Ducky 2.x

**Package:** Ducky (`buildTransitive/Ducky.targets`) and the final Ducky.Generator 2.0.0 package. **Surface:** MSBuild
error before compilation (SPEC §3, §16.2).

## What happened

The project references the `Ducky.Generator` package. In Ducky 2.x the source generators and analyzers ship inside the
`Ducky` package itself (`analyzers/dotnet/cs/Ducky.Generators.dll`); the separate 1.x generator targets the 1.x API.
Ducky's build targets stop the build when they see the reference, and the final `Ducky.Generator` 2.0.0 package contains
nothing but the same error.

## How to fix it

Remove the `Ducky.Generator` `PackageReference` (and its `PackageVersion` under central package management). Keep the
`Ducky` reference: the generators come with it. The migration guide (`docs/migration/ducky-1x.md`) lists the other
changes from 1.x.
