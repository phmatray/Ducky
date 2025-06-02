# Component Generator

The Component Generator creates strongly-typed Blazor component base classes for specific state slices. These components automatically handle state binding, action dispatching, and lifecycle management.

## Usage

```csharp
using Ducky.CodeGen.Core;

// Configure the generator
var options = new ComponentGeneratorOptions
{
    Namespace = "MyApp.Components",
    RootStateType = "AppState",
    Components = new List<ComponentDescriptor>
    {
        new ComponentDescriptor
        {
            ComponentName = "TodoStateComponent",
            StateSliceName = "Todos",
            StateSliceType = "TodoState", 
            StateSliceProperty = "Todos",
            Actions = new List<ComponentActionDescriptor>
            {
                new ComponentActionDescriptor 
                { 
                    ActionName = "AddTodo", 
                    ActionType = "AddTodoAction",
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" },
                        new ParameterDescriptor { ParamName = "title", ParamType = "string" }
                    }
                },
                new ComponentActionDescriptor 
                { 
                    ActionName = "ToggleTodo", 
                    ActionType = "ToggleTodoAction",
                    Parameters = new List<ParameterDescriptor>
                    {
                        new ParameterDescriptor { ParamName = "id", ParamType = "int" }
                    }
                }
            }
        }
    }
};

// Generate the component code
var generator = new ComponentGenerator();
var generatedCode = generator.GenerateCode(options);
```

## Generated Output

The generator produces component base classes like this:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Ducky;

namespace MyApp.Components;

// Base for any component focused on Todos
public abstract class TodoStateComponent : ComponentBase, IDisposable
{
    [Inject]
    protected IStore<AppState> Store { get; set; } = default!;
        
    /// <summary>The current TodoState slice.</summary>
    protected TodoState State => Store.GetState().Todos;

    protected override void OnInitialized()
    {
        Store.StateChanged += OnStateChanged;
        base.OnInitialized();
    }

    /// <summary>Called whenever AppState changes; triggers UI refresh.</summary>
    protected virtual void OnStateChanged(AppState appState)
        => InvokeAsync(StateHasChanged);

    /// <summary>Dispatch any action.</summary>
    protected void Dispatch(object action)
        => Store.Dispatch(action);
    
    protected void AddTodo(int id, string title)
        => Dispatch(new AddTodoAction(id, title));
    
    protected void ToggleTodo(int id)
        => Dispatch(new ToggleTodoAction(id));

    public void Dispose()
        => Store.StateChanged -= OnStateChanged;
}
```

## Using Generated Components

In your Blazor components, inherit from the generated base classes:

```razor
@page "/todos"
@inherits TodoStateComponent

<h3>Todo List</h3>

@foreach (var todo in State.Items)
{
    <div>
        <span @onclick="() => ToggleTodo(todo.Id)">
            @todo.Title (@todo.IsCompleted)
        </span>
    </div>
}

<button @onclick="() => AddTodo(Random.Shared.Next(), 'New Todo')">
    Add Todo
</button>
```

## Benefits

1. **Strong Typing**: All state access and action dispatch is strongly typed
2. **Automatic Updates**: Components automatically re-render when state changes
3. **Lifecycle Management**: Proper subscription/unsubscription handling
4. **Code Consistency**: All components follow the same patterns
5. **Reduced Boilerplate**: No need to manually write state binding code
6. **Intellisense Support**: Full IDE support for state properties and action methods