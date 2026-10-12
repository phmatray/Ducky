# DUCKY006: Type missing from the JSON context

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the `UseJson` argument, once per missing
type (SPEC §10, §16.2, ADR-0022).

## What happened

`UseJson(X.Default)` or `UseJson(JsonTypeInfoResolver.Combine(A.Default, B.Default))` was given a
`JsonSerializerContext`, and a `[DuckyAction]` type or a slice state type declared in this project is not covered by it.
Ducky serializes only through that resolver (no reflection, AOT-safe), so without type information the type is shown by
name only in DevTools and logs, and a persisted or prerendered slice fails at startup (DUCKY306).

A type is covered when the context declares a `JsonTypeInfo<T>` property for it, which is exactly what the
System.Text.Json source generator emits for every type it generates metadata for, the ones it reaches through another
type's properties included. So a state type used only as the element of a `List<T>` property of a listed type is
covered, and a type reachable only through a `[JsonIgnore]` property is not. With `Combine`, the covered types are the
union over the contexts. Any other argument (a `JsonSerializerOptions`, a resolver that is not a context, a `Combine`
that also takes such a resolver or an array variable) is not checked, and a type that names a type parameter (the
`List<T>` state of a generic `ListSlice<T>`, a generic action) is skipped: `typeof` cannot name it in a context.

## How to fix it

Add `[JsonSerializable(typeof(T))]` for each reported type to the context (or to one of the combined contexts). The
getting-started path is to add an empty context, pass it to `UseJson`, and let this warning list what it needs.
