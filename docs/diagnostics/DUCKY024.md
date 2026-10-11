# DUCKY024: Ambiguous dispatch helpers

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build info on the later action's name (SPEC §16.1, GEN-01).

## What happened

Two `[DuckyAction]` types of this compilation generate helpers with the same name and the same parameter types
(nullability aside, as overload resolution ignores it), for example `App.Cart.Increment(int By)` and
`App.Counter.Increment(int Step)`. Every action emits both `M` and `MAsync`, so `record Load(int Id)` and
`record LoadAsync(int Id)` clash too: both generate `LoadAsync(this IDispatcher, int)`. Each helper lives in its type's namespace, so code that imports both namespaces,
as `_Imports.razor` or global usings commonly do, gets CS0121 on every call. Two such actions in the same namespace are
ambiguous even without imports.

The generator compares only the actions of the compilation it runs on: it does not scan referenced assemblies for
their generated helpers (a metadata walk on every compilation would defeat incremental caching), so a clash with an
action of a referenced library surfaces as CS0121 at the call site only.

## How to fix it

Set `MethodName` on one of the two actions, for example `[DuckyAction(MethodName = "IncrementCounter")]`, or call the
helper through its class, `App.Counter.IncrementDispatchExtensions.Increment(store, 1)`.
