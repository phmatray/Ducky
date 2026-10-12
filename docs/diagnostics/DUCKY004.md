# DUCKY004: Singleton store in a server project

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the `Lifetime` assignment (SPEC §7,
§16.2, ADR-0025).

## What happened

The `AddDucky` callback sets `Lifetime = ServiceLifetime.Singleton` in a web project (`Microsoft.NET.Sdk.Web`) that is
not a Blazor WebAssembly project. On a server, a singleton store is shared by every user and every circuit: one user's
actions change what all the others see, and effects run with one DI scope for the whole process. The store also logs a
Warning (EventId 1004) when it is built on a non-browser host.

The analyzer knows the project kind from the `UsingMicrosoftNETSdkWeb` and `UsingMicrosoftNETSdkBlazorWebAssembly`
MSBuild properties, which `buildTransitive/Ducky.targets` makes visible to it. Only a constant `Singleton` is reported: a
lifetime computed at run time, such as `OperatingSystem.IsBrowser() ? ServiceLifetime.Singleton : ServiceLifetime.Scoped`,
is not.

## How to fix it

Remove the assignment. The default is `Singleton` in the browser and `Scoped` elsewhere, read after `configure` returns,
which is what a server or an Interactive Auto app needs. Set `Scoped` explicitly if you want it spelled out.
