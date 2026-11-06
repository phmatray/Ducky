# Getting started

Ducky keeps your application state in slices: immutable records, each changed only by its reducers. You dispatch actions to one store, which reduces them one at a time and notifies whoever selected the changed values.

## Install

Ducky targets .NET 10.

```shell
dotnet add package Ducky
```

**Every project that declares actions, slices or `[Draftable]` records needs its own direct `Ducky` (or `Ducky.Draft`) `PackageReference`.** Ducky's source generators ship inside the package, and generator assets never flow through a `ProjectReference`: a `PackageReference` keeps its `analyzers` and `build` assets private by default. So when `MyApp` references `MyApp.State` by `ProjectReference`, `MyApp` sees the Ducky types but runs no generator. If `MyApp` declares slices or actions too, give it a `Ducky` reference of its own, or it gets no `AddDuckyGenerated_MyApp()`, no dispatch helpers and no analyzers. [DUCKY016](../../diagnostics/DUCKY016.md) warns when a project declares slices or effects and calls `AddDucky` without its generated registration.

## Declare state, actions and a slice

State is an immutable `class`, usually a `record`. Actions are plain records. A slice declares the initial state and one reducer per action type:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/GettingStartedTests.cs#getting-started-slice)]

`[DuckyAction]` asks the generator for dispatch helpers: `store.Rename("Ada")` and `await store.RenameAsync("Ada")`. They work on anything that dispatches (the store, an effect's context, a test store).

## Register

`AddDuckyGenerated_{Assembly}()` is generated for each assembly (namespace `Ducky.Generated`; every character of the assembly name that isn't valid in an identifier becomes `_`). It registers every slice and effect the assembly declares:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/GettingStartedTests.cs#getting-started-register)]

In an app this is `builder.Services.AddDucky(...)` in `Program.cs`. A class library that declares slices has its own generated method: call both, as in `ducky.AddDuckyGenerated_MyApp().AddDuckyGenerated_MyApp_State()`. Middleware is never generated: register it with `Use<T>()`.

The store is a singleton in the browser (Blazor WebAssembly) and scoped on the server, one per request or circuit.

## Dispatch and read

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/GettingStartedTests.cs#getting-started-dispatch)]

Registration takes the slice type (`AddSlice<GreetingSlice>()`); reads take the state type (`State.Get<Greeting>()`).

A synchronous `Dispatch` reduces the action before it returns only when the calling thread became the drainer. `await DispatchAsync(...)` always gives read-your-writes: it completes once the action is reduced, before its effects finish. The [threading contract](../../threading.md) states the rules.

## Next

- [Slices and keys](slices-and-keys.md)
- [Effects and concurrency policies](effects.md)
- [Selectors](selectors.md)
- [SliceStore](slice-store.md)
- [EntityState](entity-state.md)
- [Testing with Ducky.Testing](testing.md)
