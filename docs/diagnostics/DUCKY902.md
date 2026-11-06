# DUCKY902: Ducky.Blazor 1.x next to Ducky 2.x

**Package:** Ducky (`buildTransitive/Ducky.targets`). **Surface:** MSBuild error after package assets are resolved
(SPEC §3, §16.2).

## What happened

The project resolves a `Ducky.Blazor` package whose major version is below 2 while it uses Ducky 2.x. Ducky.Blazor 1.x
declares a dependency of `Ducky >= 1.x`, which Ducky 2 satisfies, so NuGet restores the pair without complaint; at run
time the 1.x assembly calls APIs Ducky 2 removed and fails with `TypeLoadException` or `MissingMethodException`. The
build stops instead, naming the package and version it found. Ducky 1.x shipped no Ducky.Reactive or Ducky.Testing
package, so Ducky.Blazor is the only package checked.

## How to fix it

Update `Ducky.Blazor` to the same 2.x version as `Ducky`. If the 1.x package comes in transitively, through another
package, update that package or add a direct `Ducky.Blazor` 2.x reference.
