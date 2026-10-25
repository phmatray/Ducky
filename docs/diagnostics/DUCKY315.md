# DUCKY315: PersistStorage.Server with the default scope and no AuthenticationStateProvider

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A persisted slice uses `PersistStorage.Server` while `BlazorOptions.Scope` is not set, so its keys are scoped by the
default `DuckyScopes.NameIdentifier`, which reads the user's `ClaimTypes.NameIdentifier` from the
`AuthenticationStateProvider`. The app registers none. The message names the slice.

The check is not made in the browser (`OperatingSystem.IsBrowser()`): Server storage is inert there.

## How to fix it

Register authentication so an `AuthenticationStateProvider` is available (the Blazor Web App templates do it with
`AddCascadingAuthenticationState` and an authentication scheme), or set `BlazorOptions.Scope` in `AddBlazor` to a
delegate that returns the user or tenant segment of the key. A delegate that returns `null` means no reads and no writes
of Server storage, never a shared key.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
