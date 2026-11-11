<p align="center">
  <img src="build/package/icon.png" width="96" height="96" alt="Ducky">
</p>

<h1 align="center">Ducky</h1>

<p align="center"><strong>Blazor state you can explain to a duck.</strong></p>

<p align="center">
  <a href="https://www.nuget.org/packages/Ducky"><img src="https://img.shields.io/nuget/vpre/Ducky?label=NuGet" alt="NuGet"></a>
  <a href="https://github.com/phmatray/Ducky/actions/workflows/ci.yml?query=branch%3Av2"><img src="https://github.com/phmatray/Ducky/actions/workflows/ci.yml/badge.svg?branch=v2" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License: Apache-2.0"></a>
</p>

<p align="center">
  <a href="https://phmatray.github.io/Ducky/">Website</a> ·
  <a href="https://phmatray.github.io/Ducky/docs/">Docs</a> ·
  <a href="https://phmatray.github.io/Ducky/docs/tutorial/">Tutorial</a> ·
  <a href="https://phmatray.github.io/Ducky/playground/">Playground</a> ·
  <a href="https://phmatray.github.io/Ducky/docs/compare.html">How Ducky compares</a>
</p>

Ducky is a small, strict Redux store for .NET 10 and Blazor. Application state lives in slices: immutable records, each changed only by its reducers. You dispatch actions to one store, which reduces them one at a time and notifies whoever selected the changed values. Effects do the work that leaves the store, such as HTTP calls and timers. Persistence, the prerender handoff, cross-tab sync and Redux DevTools follow the same rules in every render mode.

> **Status:** 2.0 is in preview (`2.0.0-rc.1`). Install it with `--prerelease`. Ducky 1.x lives on the `main` branch; see [Migrating from Ducky 1.x](https://phmatray.github.io/Ducky/docs/migration/ducky-1x.html).

## Install

```shell
dotnet add package Ducky --prerelease
dotnet add package Ducky.Blazor --prerelease
```

## A slice in thirty seconds

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

A source generator writes the registration, so there is nothing to scan and nothing to wire by hand. In the `Program.cs` of an app named `MyApp`:

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

The [tutorial](https://phmatray.github.io/Ducky/docs/tutorial/) builds a small shop from there, step by step: components, effects, a search box, selectors, persistence and tests.

## What Ducky promises

- **One dispatcher, one action at a time.** Every action goes through one queue and a single drainer reduces it. Reducers never run concurrently, actions keep their order, and no reader sees torn state. `await DispatchAsync` completes once the action is reduced and never faults.
- **Load once, in every render mode.** Mark a slice `Persist<T>()` and `Prerender<T>()`: the prerendered page hands its state to the interactive store before the first interactive render, on WebAssembly, Interactive Server, Interactive Auto and static SSR.
- **The newest request wins.** Effects declare a concurrency policy (`Switch`, `Merge`, `Exhaust` or `Queue`). With `Switch`, a newer request for the same key cancels the running one, and a superseded run can't dispatch its late result.
- **Tabs converge, nothing echoes.** `SyncAcrossTabs` sends a `localStorage` slice's changes to the other tabs, and a value received from another tab is never written back.
- **No reflection to trim away.** Generators write the registration, the dispatch helpers and the analyzers; persistence serializes through each slice's `JsonTypeInfo`. Every runtime library builds with zero IL2xxx and IL3xxx warnings, and CI runs a NativeAOT binary and a trimmed AOT WebAssembly app.
- **Given, when, then.** `Ducky.Testing` tests a reducer in three lines, runs an effect against stubs, or spins up a whole `TestStore` with a fake clock. No browser required.

Each promise is a named invariant with the tests that prove it: see the [threading contract](https://phmatray.github.io/Ducky/docs/threading.html) and the [website](https://phmatray.github.io/Ducky/).

## Packages

| Package | What it holds |
|---|---|
| [Ducky](https://www.nuget.org/packages/Ducky) | The store, slices, effects, selectors, middleware, `SliceStore` and `EntityState`, with the source generators and analyzers. |
| [Ducky.Blazor](https://www.nuget.org/packages/Ducky.Blazor) | Components that re-render when a selected value changes, browser storage and `IDistributedCache` persistence, the prerender handoff, cross-tab sync and Redux DevTools. |
| [Ducky.Testing](https://www.nuget.org/packages/Ducky.Testing) | `SliceTest`, `EffectTest` and `TestStore` for any test framework. |
| [Ducky.Draft](https://www.nuget.org/packages/Ducky.Draft) | Immer-style `Produce` for immutable records: write the change as a mutation, get a new record that shares every unchanged part. |
| [Ducky.Reactive](https://www.nuget.org/packages/Ducky.Reactive) | R3 reactive effects for debouncing, cancelling stale requests and polling. |

## Coming from another library

Step-by-step guides with compiled before-and-after samples: [from Fluxor](https://phmatray.github.io/Ducky/docs/migration/fluxor.html), [from TimeWarp.State](https://phmatray.github.io/Ducky/docs/migration/timewarp-state.html), [from a hand-written state container](https://phmatray.github.io/Ducky/docs/migration/state-container.html) and [from Ducky 1.x](https://phmatray.github.io/Ducky/docs/migration/ducky-1x.html). The [comparison page](https://phmatray.github.io/Ducky/docs/compare.html) sets Ducky next to the alternatives, with sources.

## Quality bar

100% line and branch coverage of the library code, a 100% Stryker mutation score on all seven projects before each release, linearizability and property tests for the dispatcher, end-to-end tests in a real browser, and the NativeAOT and trimmed WebAssembly smoke apps. Every code snippet in the docs comes from a compiled, tested file. The full specification is in [docs/spec](docs/spec/README.md), and the decisions behind it are in [docs/adr](docs/adr).

## Building from source

The .NET SDK pinned in [global.json](global.json) is all you need:

```shell
./build.sh Ci      # restore, build, tests, coverage, package checks, docs
```

`./build.sh --help` lists the other targets, such as `E2E` and `Mutation`.

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
