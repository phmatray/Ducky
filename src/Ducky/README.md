# Ducky

Ducky is a state library for .NET 10 and Blazor. Application state lives in slices: immutable records, each changed only by its reducers. You dispatch actions to one store, which reduces them one at a time and notifies whoever selected the changed values. Effects do the work that leaves the store, such as HTTP calls and timers.

This package holds the store, slices, effects, selectors, middleware, `SliceStore` and `EntityState`, plus the source generators and analyzers: a generated registration method per assembly, dispatch helpers and build-time checks.

## Install

```shell
dotnet add package Ducky --prerelease
```

Reference `Ducky` directly from every project that declares actions, slices or effects. The generators ship inside this package, and generator assets never flow through a `ProjectReference`.

For a Blazor app, install [Ducky.Blazor](https://www.nuget.org/packages/Ducky.Blazor) as well.

## Example

A slice declares its initial state and one reducer per action type:

```csharp
using Ducky;

public sealed record Counter(int Value);

public sealed record Increment(int By);

public sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice() => On<Increment>((state, action) => state with { Value = state.Value + action.By });

    protected override Counter Initial => new(0);
}
```

Register the assembly's slices and effects with the generated `AddDuckyGenerated_{Assembly}()` (namespace `Ducky.Generated`). In the `Program.cs` of an app named `MyApp`:

```csharp
using Ducky;
using Ducky.Generated;

builder.Services.AddDucky(ducky => ducky.AddDuckyGenerated_MyApp()); // every slice and effect of MyApp
```

Then dispatch and read through the injected `IStore`:

```csharp
var result = await store.DispatchAsync(new Increment(2)); // completes once the action is reduced
var count = store.State.Get<Counter>().Value;             // 2
```

## Learn more

- Documentation: https://phmatray.github.io/Ducky/
- Getting started: https://phmatray.github.io/Ducky/docs/guides/core/getting-started.html
- Playground (a shop built with Ducky, running in the browser): https://phmatray.github.io/Ducky/playground/
- Migrating from Ducky 1.x: https://phmatray.github.io/Ducky/docs/migration/ducky-1x.html
- Changelog: https://github.com/phmatray/Ducky/blob/v2/CHANGELOG.md
- Issues and source: https://github.com/phmatray/Ducky

Ducky is licensed under the Apache License 2.0.
