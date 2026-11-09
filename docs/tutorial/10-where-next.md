# Where next

You built a Blazor app whose state lives in one store: slices of immutable records, changed only by reducers, one action at a time; effects for the server, with a concurrency policy each; memoized selectors for derived data; a persisted, versioned basket; and tests that run in a second. Here is where each idea goes further.

## See it bigger

- The [Ducky Playground](https://phmatray.github.io/Ducky/playground/) is Rubber Duck Supply Co., a bigger version of this shop, running in your browser. Switch on X-ray: it names the Ducky feature behind each part of the page and shows its source. You will recognise the search box on `Switch`, the memoized shelf and the persisted basket, next to a checkout on `Exhaust`, order tracking and more.
- The finished Duck Shop of this tutorial, with all its tests, is in [docs/tutorial/samples](samples).

## Go deeper, by topic

- [Slices and keys](../guides/core/slices-and-keys.md): reducer rules, exact-type matching, how keys are derived and pinned.
- [Effects and concurrency policies](../guides/core/effects.md): `EffectGroup`, concurrency keys, pollers and `LongRunning`.
- [Selectors](../guides/core/selectors.md): selections, `onChange`, selectors with an argument.
- [SliceStore](../guides/core/slice-store.md): a slice with methods, `cart.Add(line)` instead of an action, when you prefer that style.
- [EntityState](../guides/core/entity-state.md): collections looked up by id, as the playground's catalogue is.
- [Testing with Ducky.Testing](../guides/core/testing.md): everything `SliceTest`, `EffectTest` and `TestStore` check.
- [Render modes and the load rule](../guides/blazor/render-modes.md): the same store on Interactive Server, Interactive Auto and static SSR.
- [Persistence](../guides/blazor/persistence.md): `sessionStorage`, server storage, `MaxAge`, waiting for hydration, clearing on sign-out.
- [Cross-tab sync](../guides/blazor/cross-tab.md): what `SyncAcrossTabs` does in detail.
- [The threading contract](../threading.md): exactly when an action is reduced, and on which thread.

## Watch every action

[Redux DevTools](../guides/blazor/devtools.md) shows every action the store reduces, the state after each one, and lets you step back in time. Enable it with one call, `UseDevTools()`, inside your `AddDucky` callback, guarded by `builder.HostEnvironment.IsDevelopment()` so it only runs in development (it turns on whenever it is called), and install the browser extension.

## Compare and migrate

- [How Ducky compares](../compare.md) sets Ducky beside Fluxor, TimeWarp.State and a hand-written state container, including where the others are ahead.
- Coming from [Fluxor](../migration/fluxor.md), [TimeWarp.State](../migration/timewarp-state.md) or [a state container service](../migration/state-container.md)? Each guide maps your app to Ducky, step by step.
