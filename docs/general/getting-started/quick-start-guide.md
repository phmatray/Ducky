# Quick Start Guide

Welcome to the R3dux Quick Start Guide! This guide will help you get up and running with R3dux in your .NET application. By the end of this guide, you'll have a basic understanding of how to set up a project with R3dux and implement a simple state management example.

## Setting Up Your First Project

### 1. Create a New .NET Project

To get started, you'll need to create a new .NET project. You can create a console application, a Blazor application, or any other type of .NET project where you want to manage state using R3dux.

#### Using the .NET CLI

Open your terminal or command prompt and run the following commands to create a new .NET console application:

```bash
dotnet new console -n R3duxDemo
cd R3duxDemo
```

This creates a new directory called `R3duxDemo` containing a basic .NET console application.

### 2. Install R3dux

Next, you need to install R3dux via NuGet.

#### Using the .NET CLI

Run the following command to install R3dux:

```bash
dotnet add package R3dux
```

### 3. Set Up Your Project for R3dux

In your project, you'll need to define actions, reducers, and optionally effects. For this quick start, we'll keep it simple and create a counter example.

#### Define Actions

Create a new file called `CounterActions.cs`:

```csharp
public record Increment : IAction;
public record Decrement : IAction;
```

#### Define Reducers

Create a new file called `CounterReducers.cs`:

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

#### Set Up the Store

In your `Program.cs` file (or equivalent entry point), set up the R3dux store:

```csharp
using R3dux;

class Program
{
    static void Main(string[] args)
    {
        var store = new Store(new CounterReducers());

        store.Dispatch(new Increment());
        Console.WriteLine($"Counter: {store.GetState<int>()}");

        store.Dispatch(new Increment());
        Console.WriteLine($"Counter: {store.GetState<int>()}");

        store.Dispatch(new Decrement());
        Console.WriteLine($"Counter: {store.GetState<int>()}");
    }
}
```

## Basic Usage Example

Now that your project is set up, let's go over the basic usage of R3dux by running the counter example.

### 1. Run the Application

You can run your application by executing the following command in your terminal:

```bash
dotnet run
```

### 2. Observe the Output

After running the application, you should see the following output:

```bash
Counter: 1
Counter: 2
Counter: 1
```

### 3. Understanding the Example

- **Actions**: `Increment` and `Decrement` are simple actions that describe state changes. These are dispatched to the store to trigger state updates.
- **Reducers**: The `CounterReducers` class defines how the state should change in response to each action. The `On` method maps actions to state changes.
- **Store**: The `Store` class manages the application state. You can dispatch actions to the store and retrieve the current state using `GetState`.

This example demonstrates how to set up a simple counter using R3dux. The state is managed in a predictable and immutable way, and actions are the only way to modify the state.
