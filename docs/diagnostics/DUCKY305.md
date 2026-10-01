# DUCKY305: Two slices share a state type

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

Two registered slices hold the same state type. Reads go through the state type (`State.Get<TState>()`,
`WasRestored<TState>()`), so each state type must belong to exactly one slice.

## How to fix it

Give one of the slices its own state type, for example a distinct record, even when the two have the same shape.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
