# Redux DevTools

The [Redux DevTools](https://github.com/reduxjs/redux-devtools) browser extension shows every action and the state after it, and travels back in time. `UseDevTools()` turns it on whenever it is called, so guard it with your host's environment:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Blazor/DevToolsGuideTests.cs#devtools-register)]

In a WebAssembly app the guard is `builder.HostEnvironment.IsDevelopment()`. Without the extension, or outside an interactive store (prerender, static SSR), DevTools stays inert.

## Options

| Option | Default | |
|---|---|---|
| `Name` | `"Ducky"` | the instance name in the extension |
| `MaxAge` | 50 | the history kept, here and in the extension |
| `Filter` | all actions | `context => bool`: which actions are sent (replaces 1.x `ShouldLogAction` and `ExcludedActionTypes`) |
| `ActionSanitizer` | none | `(actionType, payload) => payload`, applied to every action sent |
| `StateSanitizer` | none | `(sliceKey, state) => state`, applied to every state sent, the first one included |
| `Trace`, `TraceLimit` | off, 10 | attach the dispatching stack trace |

Actions and states are serialized through your `UseJson` context: an action type missing from it is sent with a `null` payload and `meta.unserializable`. Failures (`EffectFailed`, `ReducerFailed`, `HydrationFailed`, `PersistenceFailed`) show their exception's type and message. The `@ducky/persistence` slice is shown, so you can see hydration progress.

## Time travel

Jumping to an earlier action, Reset and Import restore your slices (never the `@ducky/*` ones). While the extension shows an earlier state, new actions from components and effects, late effect results included, are dropped, so the state you inspect doesn't move; jumping back to the latest action resumes. A `SliceStore` method call (`cart/Add`) can be jumped to but not replayed: its mutator is a closure, so the extension receives a `null` payload with `meta.unserializable`. A persisted slice that had a write pending may store the state you jumped to: this is a development tool.
