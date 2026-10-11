# DUCKY013: Concurrency key without value equality

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the returned expression (SPEC §5.5,
§16.2).

## What happened

An override of `Effect<TAction>.ConcurrencyKey` returns a class or an array whose type does not override `Equals`
anywhere in its hierarchy (so it is not a record either). `Policy` relates only the runs whose keys are equal, and such
a key is equal only to itself: a new instance per action means no two runs ever share a key, so `Switch` never cancels
and `Exhaust` never drops anything.

A value whose static type is `object` or an interface has an unknown runtime type and is not reported; neither is a
value returned by a lambda or local function inside the override.

## How to fix it

Return a value with value equality: a value type such as `a.Id` or the tuple `(a.TenantId, a.Id)`, a `string`, a record
such as `new SearchKey(a.Query)`, or a class that overrides `Equals` and `GetHashCode`. Return `null` for one global
slot.
