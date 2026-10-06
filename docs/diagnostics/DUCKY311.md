# DUCKY311: Migration chain gap

**Package:** Ducky.Blazor. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A persisted slice's `PersistOptions.Version` is above 1, but some step from version 1 to `Version - 1` was never
added with `Migrate(fromVersion, step)`, for example `Version = 3` without `Migrate(2, …)`. A stored state of an
older version is migrated one version at a time, so without the step it could never reach the current version. The
message names the slice, its version and every missing step. The check runs on the composed options, so a step added by
a later `Persist<TSlice>` call counts.

## How to fix it

Add the missing `Migrate(fromVersion, step)` calls to `Persist<TSlice>`. To drop the states of an old version
instead of migrating them, add a step that throws: a stored state whose migration throws is ignored with a Warning, and
the slice keeps its current state.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
