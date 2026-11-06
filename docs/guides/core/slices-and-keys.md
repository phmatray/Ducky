# Slices and keys

A slice owns one piece of state: its initial value and the reducers that turn an action into the next value.

## Reducers

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SlicesGuideTests.cs#slice-settings)]

- `On<TAction>((state, action) => ...)` reduces with the action; `On<TAction>(state => ...)` when the action carries no data. Register them in the constructor, one per action type.
- Actions match by **exact type**: a handler for `ThemeChanged` never runs for a type derived from it. An abstract type, an interface or `object` is rejected (DUCKY005).
- Return the same `state` reference when nothing changes. The store compares references, so an unchanged reference commits nothing and notifies nobody. `state with { }` is always a new reference, so it always commits and re-runs that slice's selections. Their `onChange` still fires only when the projected value changed.
- Reducers must be pure: no I/O, no clock, no random, no mutation of the state or the action. The analyzers flag these (DUCKY001 to DUCKY003). Work that leaves the store belongs in an [effect](effects.md).
- A reducer that throws commits nothing: the store dispatches `ReducerFailed` and keeps running.
- State types are classes (`TState : class`), and two slices can't share one.

## Keys

Every slice has a key, used by persistence, cross-tab sync, DevTools and `State.Get(string)`. By default it is derived from the class name: one trailing `Slice`, `Store`, `Reducers` or `Reducer` is stripped, then the name is kebab-cased.

| Class | Key |
|---|---|
| `TodoSlice` | `todo` |
| `ShoppingCartReducers` | `shopping-cart` |
| `UIStateSlice` | `ui-state` |
| `CartStore` | `cart` |
| `Features.TodosSlice` (nested) | `features-todos` |

Override `Key` to pin it, so that renaming the class doesn't change it (persisted data is stored under the key). A key is lowercase letters and digits in `-`-separated words. A generic slice class must override `Key`.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SlicesGuideTests.cs#slice-pinned-key)]

Two slices with the same key are a configuration error, reported at the first store resolution with every other problem found.

## Register and read

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SlicesGuideTests.cs#slice-register-read)]

`AddDuckyGenerated_{Assembly}()` makes the `AddSlice` calls for you. Each slice is also injectable: `AddSlice<SettingsSlice>()` registers `SettingsSlice` as the store-owned instance.

## Action names

DevTools and `ActionContext.ActionType` show an action as `Namespace.Name`. `[ActionType("settings/theme-changed")]` gives it a readable name instead, as on `ThemeChanged` above.
