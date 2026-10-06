# DUCKY316: Timeouts in the wrong order

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

One of these orders does not hold, on the final options (every `AddBlazor` delegate applied):

- `BlazorOptions.HydrationTimeout` must be shorter than `DuckyBuilder.InitTimeout`. Otherwise init aborts first,
  every action dispatched meanwhile waits for the init timeout, and hydration ends as a failure.
- `BlazorOptions.PrerenderSeedWaitTimeout` must be shorter than `BlazorOptions.HydrationTimeout`. Otherwise hydration
  times out while it still waits for the prerender seed.

The message names both values. Each order that does not hold is reported once.

## How to fix it

Lower the first value of the pair, or raise the second one.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
