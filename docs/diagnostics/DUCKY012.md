# DUCKY012: Persisted slice relies on its derived key

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build info on the `[Persist]` attribute and on a
`Persist<T>()` call of Ducky.Blazor (SPEC §16.2).

## What happened

A slice marked `[Persist]`, or passed to `Persist<T>()`, neither overrides `Key` nor inherits an override of it, so it
is stored under the key derived from its type name (ADR-0031: `CartSlice` gives `cart`). Renaming the class, or moving
it into another class, changes that key, and the data users already stored under the old key is silently orphaned.
Hiding `Key` with `new` does not change the key the store uses.

## How to fix it

Pin the key by overriding it in the slice, as in `public override string Key => "cart";`, keeping the derived value
when data was already stored under it. Pinned keys follow the same rules as derived ones: unique in the store
(DUCKY304), and `@ducky/` is reserved for library slices.
