# DUCKY025: JsonTypeInfoResolver.Combine drops the context options

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the context inside `Combine` (SPEC §10,
§16.2, ADR-0022).

## What happened

`UseJson(JsonTypeInfoResolver.Combine(...))` combines a `JsonSerializerContext` whose `[JsonSourceGenerationOptions]`
sets a non-default option: a named argument (naming policy, converters, number handling, ignore conditions...) or a
`JsonSerializerDefaults` value other than `General`, such as `JsonSerializerDefaults.Web`. `UseJson` copies a single
context's options, but `Combine` carries none, so the store falls back to default options and those settings silently
stop applying to persisted, seed and DevTools JSON. `GenerationMode` alone is not reported: it is a source-generation
setting, not a serializer option, and `Combine` loses nothing of it. An app growing from one context to two would switch
its persisted JSON from camelCase to PascalCase, and existing stored data would deserialize "successfully" with every
renamed property at its default.

## How to fix it

Pass options built from the context whose settings you want, with the combined resolver: `UseJson(new
JsonSerializerOptions(A.Default.Options) { TypeInfoResolver = JsonTypeInfoResolver.Combine(A.Default, B.Default) })`.
The warning's message spells it out with your context and the `Combine` resolvers, on one line.
