# Step 1: Create the app and add Ducky

## What you will build in this step

A new Blazor WebAssembly app with Ducky installed and its store registered. The page doesn't change yet; the store is ready for the slices of the next steps.

## Why

The **store** is the one object that holds your app's state. Blazor creates services for you through dependency injection, so the store is registered like any other service, once, in `Program.cs`. Every component and effect then gets the same store.

## Create the app

Create an empty Blazor WebAssembly app and go into its folder:

```shell
dotnet new blazorwasm --empty -o DuckShop
cd DuckShop
```

Add the two Ducky packages. `Ducky` is the store itself, and it brings the source generators that write registration code for you. `Ducky.Blazor` connects the store to Blazor components and to the browser's storage:

```shell
dotnet add package Ducky --prerelease
dotnet add package Ducky.Blazor --prerelease
```

`Ducky.Blazor` would bring `Ducky` along on its own; the direct reference states that this project declares state of its own, which needs the generators. Ducky 2.0 is in preview, so `--prerelease` is needed for now.

## Register the store

Open `Program.cs`. Add these `using` lines at the top, below the template's own:

[!code-csharp[](samples/DuckShop/Program.cs#program-usings)]

`Ducky` has the store, `Ducky.Blazor` the Blazor parts, and `Ducky.Generated` the code the generator writes. The template's `using DuckShop;` stays: the next steps put their code in that namespace.

Then register the store, after the line that adds the `App` root component and before `await builder.Build().RunAsync();`:

[!code-csharp[](samples/DuckShop.Tests/Steps/Step1/Program.cs#step1-register)]

- `AddDucky` registers the store. Everything about the store is configured inside its callback, on the `ducky` builder.
- `AddDuckyGenerated_DuckShop()` is written by Ducky's source generator while your project builds. It registers every slice and effect your project declares, so you never list them by hand. The name ends with your assembly name: a project named `My.Shop` gets `AddDuckyGenerated_My_Shop()`.
- `AddBlazor()` adds Ducky's browser side: the persistence of step 8 and the hand-off from prerendering. Components work without it; registering it now means step 8 only adds lines.

Last, open `_Imports.razor` and add the two Ducky namespaces at the end, so every component can use them:

[!code-razor[](samples/DuckShop/_Imports.razor#imports-ducky)]

## Run it

```shell
dotnet watch
```

The browser opens on the template's page: **Hello, world!** and **Welcome to your new app.** Nothing visible comes from Ducky yet, and that is expected. The build succeeded, so the generator ran: `AddDuckyGenerated_DuckShop()` exists. Leave `dotnet watch` running; it rebuilds and reloads the page as you save files in the next steps.

Some changes can't be applied while the app runs, such as a new registration in `Program.cs` or a component that changes its `@inherits`. `dotnet watch` then asks to restart: answer `a` (always). If the page doesn't match a step's **Run it**, reload it, or press Ctrl+R in the terminal to restart.

## Checkpoint

- Where does the store come from when a component needs it? (From dependency injection: `AddDucky` registered it.)
- Why don't you list your slices in `Program.cs`? (The generated `AddDuckyGenerated_DuckShop()` registers every slice and effect of the project.)
- How many times is `AddDucky` called? (Once. The next steps add to this call.)

## Common mistake

**Leaving out `--prerelease`.** Without it, NuGet installs Ducky 1.x, an older library with a different API, and the code of this step already fails to build against it. `dotnet list package` shows the version you have: it must start with 2. Remove the two packages (`dotnet remove package Ducky.Blazor`, then `Ducky`) and add them again with `--prerelease`.

**A second `AddDucky` call.** Later steps add to the store's configuration. Add each line to this one `AddDucky` callback: a second `builder.Services.AddDucky(...)` throws a `DuckyConfigurationException` at startup, [DUCKY300](../diagnostics/DUCKY300.md).

Next: [Step 2: Your first slice](02-first-slice.md).
