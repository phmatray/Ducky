# Ducky

Ducky is a state library for .NET and Blazor: slices of immutable state, reduced by actions on a single dispatcher, with effects for the work that leaves the store.

## Quick start

Reference the `Ducky` package directly from every project that declares actions or slices. A slice declares its initial state and one reducer per action type:

[!code-csharp[](../test/Ducky.Docs.Tests/QuickStartTests.cs#quick-start-slice)]

Register the assembly's slices and effects with the generated `AddDuckyGenerated_{Assembly}()` (namespace `Ducky.Generated`), initialize the store and dispatch:

[!code-csharp[](../test/Ducky.Docs.Tests/QuickStartTests.cs#quick-start-dispatch)]

Every snippet on this site is a region of a compiled, tested file.
