# DUCKY350: Unregistered state type

**Package:** Ducky. **Surface:** `DuckyConfigurationException` thrown by `State.Get<TState>()`.

## What happened

`State.Get<TState>()` was called for a state type that no registered slice holds. Registration takes the slice type
(`AddSlice<CartSlice>()`), reads take the state type (`State.Get<CartState>()`).

## How to fix it

Call the generated `AddDuckyGenerated_{Assembly}()` of the assembly that declares the slice inside `AddDucky` (the
message names the likely one), or register the slice with `AddSlice<TSlice>()`.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
