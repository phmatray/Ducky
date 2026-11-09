# DUCKY318: Negative MaxDispatchDepth

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

`DuckyBuilder.MaxDispatchDepth` was set to a negative value. An action whose synchronous causal depth is above
`MaxDispatchDepth` is dropped, and the store reports the drop with a `ReducerFailed(DispatchLoopException)` enqueued at
depth 0. With a negative value that report is itself above the limit, so it would be dropped and reported again, and
the store would never stop.

## How to fix it

Set `MaxDispatchDepth` to 0 or more, or leave it unset to get the default of 64. With 0, only root actions are
reduced: every action dispatched synchronously while another is being processed is dropped.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
