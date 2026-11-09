# Ducky

Ducky is a state library for .NET and Blazor: slices of immutable state, reduced by actions on a single dispatcher, with effects for the work that leaves the store.

## Quick start

Reference the `Ducky` package directly from every project that declares actions or slices. A slice declares its initial state and one reducer per action type:

[!code-csharp[](../test/Ducky.Docs.Tests/QuickStartTests.cs#quick-start-slice)]

Register the assembly's slices and effects with the generated `AddDuckyGenerated_{Assembly}()` (namespace `Ducky.Generated`), initialize the store and dispatch:

[!code-csharp[](../test/Ducky.Docs.Tests/QuickStartTests.cs#quick-start-dispatch)]

Every snippet on this site is a region of a compiled, tested file.

## Where to go next

- [The tutorial](tutorial/index.md) builds a small duck shop from an empty Blazor WebAssembly app to a tested one, one idea per step, for readers new to Redux-style stores.
- [Getting started](guides/core/getting-started.md) builds a first slice, an effect and a component, step by step.
- [Effects and concurrency policies](guides/core/effects.md) covers the part most state libraries leave to you: what happens when an action arrives while its effect is still running.
- [Render modes and the load rule](guides/blazor/render-modes.md) shows the one rule that works in WebAssembly, Interactive Server, Interactive Auto and static SSR.
- [How Ducky compares](compare.md) sets Ducky beside Fluxor, TimeWarp.State and a hand-written state container, including where the others are ahead; [Migrating from Fluxor](migration/fluxor.md) maps a Fluxor app to Ducky.
- The playground, linked at the top of every page, is a small shop built only from Ducky: switch on X-ray to see which feature handles each part, with its source.
