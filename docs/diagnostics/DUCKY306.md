# DUCKY306: Missing JSON type information

**Package:** Ducky. **Surface:** `DuckyConfigurationException` at first store resolution.

## What happened

A type registered with `RequireJsonTypeInfo` (directly, or through `Persist<T>()` and `Prerender<T>()`) has no
`JsonTypeInfo` in the resolver passed to `UseJson`, or `UseJson` received `JsonSerializerOptions` whose
`TypeInfoResolver` is null. Ducky serializes only through `JsonTypeInfo`, so it stays trim and NativeAOT safe.

## How to fix it

Add `[JsonSerializable(typeof(YourType))]` to your `JsonSerializerContext`; the message spells out the exact line.
When the resolver is null, set `TypeInfoResolver` on the options, or call `UseJson` with your
`JsonSerializerContext.Default`.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
