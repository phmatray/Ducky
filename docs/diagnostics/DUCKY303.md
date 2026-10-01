# DUCKY303: Invalid or reserved slice key

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A slice overrides `Key` with a value that is not a valid key, or that uses the `@ducky/` prefix reserved for
Ducky's own packages. Keys appear in persistence storage keys, DevTools and logs, so they are restricted to lower-case
kebab-case.

## How to fix it

Return a key that matches `^[a-z0-9]+(-[a-z0-9]+)*$` from `Key`, for example `shopping-cart`. Only assemblies
signed with Ducky's own key may use the `@ducky/` prefix.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
