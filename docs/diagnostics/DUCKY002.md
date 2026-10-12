# DUCKY002: I/O or blocking in a reducer, mutator or projector

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the call (SPEC §16.2).

## What happened

A reducer passed to `On<T>`, a mutator passed to `SliceStore.Set`/`SetAsync` or a projector passed to
`Selector.Define` does I/O or blocks a thread: any member of a
type in `System.IO` or a namespace under it (`File`, `Path`, `new FileStream(…)`…), any member of `HttpClient`, of
`IJSRuntime` or of a type implementing it, extension methods on those included (`http.GetFromJsonAsync`,
`js.InvokeVoidAsync`), any member of `Console`, `Task<T>.Result`, `ValueTask<T>.Result`, `Task.Wait`,
`GetAwaiter().GetResult()` on any awaiter, or `await`, `await foreach` and `await using` (in a nested async lambda: a
reducer itself can't be async). A `nameof` operand evaluates nothing and is not reported.

Reducers run on the store's single drainer, under the dispatch of every action: a blocking call stalls every other
dispatch, and on Blazor WebAssembly `.Result` or `.Wait()` deadlocks the only thread. I/O also makes the result depend
on the outside world, so it can't be replayed or tested.

Only the lambda written directly as the `On<T>` reducer, the `Set`/`SetAsync` mutator or the `Define` projector is
analysed, nested lambdas inside
it included. A method group, a delegate stored in a variable, effects and generated code are not analysed.

## How to fix it

Move the work to an effect: `Effect<T>` or `EffectGroup.On<T>` handlers are async, get a cancellation token and
`ctx.Dispatch` the result as an action (for example `ProductsLoaded`), which a pure reducer then folds into the state.
In a `SliceStore`, do the work in the store method, outside the lambda, and pass only its result to `Set`
(`var items = await api.LoadAsync(ct); Set(s => s with { Items = items });`).
