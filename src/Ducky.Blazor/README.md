# Ducky.Blazor

Ducky.Blazor connects the [Ducky](https://www.nuget.org/packages/Ducky) store to Blazor. Components re-render only when a value they selected changes, and the same store runs in every host: WebAssembly, Server and Auto, with or without prerendering.

It adds:

- `DuckyComponent`, `DuckyComponent<TState>`, `DuckyLayout` and `<DuckySelect>`;
- persistence of chosen slices to `localStorage`, `sessionStorage` or a server-side `IDistributedCache`, with versioned migrations;
- the prerender handoff: state built while prerendering seeds the interactive store, so data loads once per page;
- cross-tab sync of slices persisted to `localStorage`, and the Redux DevTools browser extension.

## Install

```shell
dotnet add package Ducky.Blazor --prerelease
```

Ducky.Blazor brings Ducky, its source generators and its analyzers. A class library that declares its own slices or actions still needs a direct `Ducky` reference.

## Example

Register Ducky and its Blazor services in the `Program.cs` of an app named `MyApp`:

```csharp
using Ducky;
using Ducky.Blazor;
using Ducky.Generated;

builder.Services.AddDucky(ducky => ducky
    .AddDuckyGenerated_MyApp() // every slice and effect of MyApp
    .AddBlazor());
```

A slice:

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

A component that shows it and dispatches (add `@using Ducky.Blazor` to `_Imports.razor`):

```razor
@inherits DuckyComponent<Counter>

<p>Count: @State.Value</p>
<button @onclick="() => Dispatch(new Increment(1))">Increment</button>
```

## Learn more

- Render modes and the load rule: https://phmatray.github.io/Ducky/docs/guides/blazor/render-modes.html
- Persistence: https://phmatray.github.io/Ducky/docs/guides/blazor/persistence.html
- Prerender: https://phmatray.github.io/Ducky/docs/guides/blazor/prerender.html
- Documentation: https://phmatray.github.io/Ducky/
- Playground (a shop built with Ducky, running in the browser): https://phmatray.github.io/Ducky/playground/
- Changelog: https://github.com/phmatray/Ducky/blob/v2/CHANGELOG.md
- Issues and source: https://github.com/phmatray/Ducky

Ducky.Blazor is licensed under the Apache License 2.0.
