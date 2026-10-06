# DUCKY310: Persisted or prerendered slice not added

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

`Persist<TSlice>()` (or a `[Persist]` attribute) or `Prerender<TSlice>()` names a slice that was never added to the
store with `AddSlice<TSlice>()`, so there is nothing to persist or seed. The message names the call and the slice.

## How to fix it

Call `AddSlice<TSlice>()` in `AddDucky` (or the generated `AddDuckyGenerated_{Assembly}()` that adds it), or remove
the `Persist<TSlice>`/`Prerender<TSlice>` call.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
