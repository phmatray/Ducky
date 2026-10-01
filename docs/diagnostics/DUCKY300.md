# DUCKY300: AddDucky called twice

**Package:** Ducky. **Surface:** `DuckyConfigurationException` thrown by the second `AddDucky` call.

## What happened

`AddDucky` was called a second time on the same `IServiceCollection`. Each service collection holds one Ducky
configuration, so a second call would silently split or replace it. This is the only error `AddDucky` throws itself;
every other rule is checked at first store resolution.

## How to fix it

Call `AddDucky` once and register every slice, effect and middleware inside its `configure` callback. Extension
packages and generated registrations (`AddDuckyGenerated_{Assembly}`) are called on the `DuckyBuilder` inside that
callback, never as a second `AddDucky`.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
