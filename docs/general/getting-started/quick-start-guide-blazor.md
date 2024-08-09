# Quick Start Guide for Blazor

This guide will help you set up R3dux in a Blazor application. By following these steps, you'll integrate R3dux for state management and implement a simple counter example.

## Create Project & Install R3dux

### 1. Create a New Blazor Project

First, create a new Blazor WebAssembly project using the .NET CLI:

```bash
dotnet new blazorwasm -o BlazorApp
cd BlazorApp
```

### 2. Install R3dux and R3dux.Blazor

Next, install R3dux and the R3dux.Blazor package using the .NET CLI:

```bash
dotnet add package R3dux
dotnet add package R3dux.Blazor
```

## Prepare Startup

To configure R3dux in your Blazor application, you'll need to update your `Program.cs` file.

### 1. Configure R3dux in `Program.cs`

Open the `Program.cs` file and add the R3dux services to the Blazor service container.

#### Basic Configuration

If your slices are automatically discoverable, you can use the basic configuration:

```csharp
using R3dux.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Add R3dux with automatic assembly scanning
builder.Services.AddR3dux(builder.Configuration);

await builder.Build().RunAsync();
```

#### Advanced Configuration

If you want more control, for example, specifying which assemblies to scan for slices, use the following configuration:

```csharp
using R3dux.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Add R3dux with specific assemblies
builder.Services.AddR3dux(options =>
{
    options.Assemblies = new[] { typeof(CounterSlice).Assembly };
});

await builder.Build().RunAsync();
```

## Implement a Counter Example

Now that R3dux is set up, let’s implement a simple counter example in your Blazor application.

### 1. Define Actions

Create a new file called `CounterActions.cs` in the `Shared` folder:

```csharp
public record Increment : IAction;
public record Decrement : IAction;
```

### 2. Define Reducers

Create a new file called `CounterReducers.cs` in the `Shared` folder:

```csharp
public class CounterReducers : SliceReducers<int>
{
    public CounterReducers()
    {
        On<Increment>((state, _) => state + 1);
        On<Decrement>((state, _) => state - 1);
    }

    public override int GetInitialState()
    {
        return 0;
    }
}
```

### 3. Update the Counter Component

Now, let's modify the default `Counter.razor` component to use R3dux.

#### Update `Counter.razor`

Replace the contents of the `Counter.razor` component with the following:

```razor
@page "/counter"
@inherits R3duxComponent<int>

<h3>Counter</h3>

<p>Current count: @State</p>

<button class="btn btn-primary" @onclick="Increment">Increment</button>
<button class="btn btn-secondary" @onclick="Decrement">Decrement</button>

@code {
    private void Increment()
    {
        Dispatch(new Increment());
    }

    private void Decrement()
    {
        Dispatch(new Decrement());
    }
}
```

### 4. Run Your Blazor Application

You can now run your Blazor application using the following command:

```bash
dotnet run
```

### 5. View the Counter Component

Navigate to `/counter` in your web browser. You should see a counter that increments and decrements as you click the respective buttons.

### Understanding the Example

- **Actions**: The `Increment` and `Decrement` actions are simple records that describe state changes.
- **Reducers**: The `CounterReducers` class defines how the state changes in response to the dispatched actions.
- **R3duxComponent**: The `Counter.razor` component inherits from `R3duxComponent<int>`, automatically binding the state to the component.
- **Dispatch**: The `Dispatch` method is used to send actions to the store, triggering state updates.
