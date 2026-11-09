# Ducky.Generator (deprecated)

Ducky.Generator is deprecated. From Ducky 2.0, the source generators and analyzers ship inside the [Ducky](https://www.nuget.org/packages/Ducky) package itself.

This 2.0.0 version is the last one, and it contains no generator: a project that references it fails to build with DUCKY900, which names the fix.

## What to do

1. Remove the `Ducky.Generator` package reference.
2. Reference `Ducky` 2.x directly from every project that declares actions, slices or effects (Blazor apps: `Ducky.Blazor` 2.x, which brings `Ducky`).
3. Follow the migration guide for the rest of the 1.x to 2.0 changes.

## Links

- Migrating from Ducky 1.x: https://phmatray.github.io/Ducky/docs/migration/ducky-1x.html
- DUCKY900: https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/DUCKY900.md
- Documentation: https://phmatray.github.io/Ducky/
- Issues and source: https://github.com/phmatray/Ducky
