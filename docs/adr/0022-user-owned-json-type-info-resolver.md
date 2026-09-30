# ADR-0022: User-owned JSON type-info resolver

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §10, §16.2; CORE-07; open question 8; review rounds 4, 5, 6 and 7

## Context

A Ducky generator can't emit a `JsonSerializerContext` (the STJ generator never sees generator output), and JSInterop serializes `object[]` by reflection with warnings suppressed.

## Decision

All serialization goes through the options fixed by `UseJson`. `UseJson(JsonSerializerOptions)` takes the user's options (resolver required, reported as DUCKY306 at first resolution with the other errors); the store freezes a copy, never the caller's instance, which the app may share (review round 4); `UseJson(resolver)` given a `JsonSerializerContext` copies the context's own options, so its `[JsonSourceGenerationOptions]` (naming policy, converters such as `JsonStringEnumConverter<T>`) apply (review round 3). `TryDeserialize` takes an optional key so its Warning names the slice or storage key (review round 4). Deserialization never throws: any non-fatal exception, including one from a state constructor, returns false and the slice keeps its current state. Serialization (`TrySerialize`, `ToNode`) catches every non-fatal exception too; only missing type info is cached as unserializable, while a value-dependent exception (a NaN, a cyclic instance) fails only that call, so persistence reports `PersistenceFailed` and writes the next valid value (recovered review round 2). Values are pre-serialized to strings before interop. Without type info, DevTools and logs show names only; `Persist`/`Prerender` without type info fail at startup (DUCKY306). DUCKY006 catches omissions at build time, checked against the types the context actually covers rather than the attribute list itself, so a type reachable from another root is not a false positive (review round 5); review round 7 defines that set observably, as the `T`s for which the context type declares a `JsonTypeInfo<T>` property (what the STJ generator emits, and exactly what `GetTypeInfo` answers), so the analyzer never re-implements STJ's traversal and agrees with DUCKY306 by construction. `JsonTypeInfoResolver.Combine(...)` carries no options, so any other resolver gets default options and the combined contexts' `[JsonSourceGenerationOptions]` do not apply; DUCKY025 (Warning) reports `UseJson(Combine(...))` when a combined context sets a non-default option, through a named argument or the `JsonSerializerDefaults` constructor argument (`JsonSerializerDefaults.Web`, review round 7), and gives the fix, `new JsonSerializerOptions(A.Default.Options) { TypeInfoResolver = Combine(...) }`, since an app growing from one context to two would otherwise switch its persisted JSON's naming silently (review round 6).

## Consequences

An honest AOT/trim story; one place to configure JSON.

## Alternatives considered

Reflection-based STJ (rejected: AOT); generator-emitted context (rejected: impossible).
