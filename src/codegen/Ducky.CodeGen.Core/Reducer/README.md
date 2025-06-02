# Reducer Generator

The Reducer Generator creates strongly-typed reducer classes with partial method signatures, allowing developers to implement custom business logic while maintaining type safety and consistency.

## Usage

```csharp
using Ducky.CodeGen.Core;

// Configure the generator
var options = new ReducerGeneratorOptions
{
    Namespace = "MyApp.Reducers",
    StateType = "TodoState",
    ReducerName = "TodoReducer",
    Actions = new List<ActionDescriptor>
    {
        new ActionDescriptor
        {
            ActionName = "AddTodoAction",
            Parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor { ParamName = "id", ParamType = "int" },
                new ParameterDescriptor { ParamName = "title", ParamType = "string" }
            }
        },
        new ActionDescriptor
        {
            ActionName = "ToggleTodoAction", 
            Parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor { ParamName = "id", ParamType = "int" }
            }
        },
        new ActionDescriptor
        {
            ActionName = "RemoveTodoAction",
            Parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor { ParamName = "id", ParamType = "int" }
            }
        }
    }
};

// Generate the reducer code
var generator = new ReducerGenerator();
var generatedCode = generator.GenerateCode(options);
```

## Generated Output

The generator produces reducer classes with partial method signatures:

```csharp
using System;
using Ducky;
using MyApp.Actions;
using MyApp.State;

namespace MyApp.Reducers;

public partial class TodoReducer : SliceReducers<TodoState>
{
    public TodoReducer() : base(GetInitialState())
    {
        On<AddTodoAction>((state, action) => HandleAddTodo(state, action));
        On<ToggleTodoAction>((state, action) => HandleToggleTodo(state, action));
        On<RemoveTodoAction>((state, action) => HandleRemoveTodo(state, action));
    }

    /// <summary>
    /// Gets the initial state for TodoState.
    /// </summary>
    private static TodoState GetInitialState() => new();

    /// <summary>
    /// Handles AddTodoAction to update the state.
    /// </summary>
    private static partial TodoState HandleAddTodo(TodoState state, AddTodoAction action);

    /// <summary>
    /// Handles ToggleTodoAction to update the state.
    /// </summary>
    private static partial TodoState HandleToggleTodo(TodoState state, ToggleTodoAction action);

    /// <summary>
    /// Handles RemoveTodoAction to update the state.
    /// </summary>
    private static partial TodoState HandleRemoveTodo(TodoState state, RemoveTodoAction action);
}
```

## Implementation

Developers implement the partial methods to provide custom business logic:

```csharp
// TodoReducer.Implementation.cs
using MyApp.State;
using MyApp.Actions;

namespace MyApp.Reducers;

public partial class TodoReducer
{
    private static partial TodoState HandleAddTodo(TodoState state, AddTodoAction action)
    {
        var newTodo = new TodoItem(action.Id, action.Title, false);
        return state with 
        { 
            Items = state.Items.Add(action.Id, newTodo),
            IsLoading = false,
            Error = null
        };
    }

    private static partial TodoState HandleToggleTodo(TodoState state, ToggleTodoAction action)
    {
        if (!state.Items.TryGetValue(action.Id, out var todo))
        {
            return state; // Todo not found
        }

        var updatedTodo = todo with { IsCompleted = !todo.IsCompleted };
        return state with 
        { 
            Items = state.Items.Update(action.Id, updatedTodo)
        };
    }

    private static partial TodoState HandleRemoveTodo(TodoState state, RemoveTodoAction action)
    {
        return state with 
        { 
            Items = state.Items.Remove(action.Id)
        };
    }
}
```

## Features

### 1. Partial Methods
- Generates partial method signatures for each action
- Developers implement the actual reduction logic
- Compile-time safety ensures all methods are implemented

### 2. Type Safety
- Strongly typed state and action parameters
- Compile-time verification of reducer signatures
- IntelliSense support for all types

### 3. Automatic Registration
- Constructor automatically registers action handlers
- Uses `On<TAction>()` pattern for registration
- Maintains proper action-to-handler mapping

### 4. Documentation
- Generates XML documentation for all methods
- Describes the purpose of each handler
- Provides context for implementation

## Benefits

1. **Type Safety**: All reducer methods are strongly typed
2. **Consistency**: All reducers follow the same pattern
3. **Separation of Concerns**: Generated structure separate from business logic
4. **Compile-Time Verification**: Missing implementations cause compile errors
5. **IntelliSense**: Full IDE support for reducer development
6. **Maintainability**: Clear separation between generated and custom code

## Reducer Patterns

### Simple State Updates
```csharp
private static partial TodoState HandleMarkComplete(TodoState state, MarkCompleteAction action)
{
    return state with { IsCompleted = true };
}
```

### Conditional Updates
```csharp
private static partial TodoState HandleToggleTodo(TodoState state, ToggleTodoAction action)
{
    if (!state.Items.ContainsKey(action.Id))
    {
        return state; // No change if item doesn't exist
    }
    
    // Perform the toggle logic
    return UpdateTodoItem(state, action.Id, item => item with { IsCompleted = !item.IsCompleted });
}
```

### Error Handling
```csharp
private static partial TodoState HandleDeleteTodo(TodoState state, DeleteTodoAction action)
{
    try
    {
        return state with 
        { 
            Items = state.Items.Remove(action.Id),
            Error = null 
        };
    }
    catch (Exception ex)
    {
        return state with { Error = ex.Message };
    }
}
```

### Complex State Transformations
```csharp
private static partial TodoState HandleBulkUpdate(TodoState state, BulkUpdateAction action)
{
    var updatedItems = state.Items;
    
    foreach (var update in action.Updates)
    {
        if (updatedItems.TryGetValue(update.Id, out var item))
        {
            var updatedItem = item with 
            { 
                Title = update.Title ?? item.Title,
                IsCompleted = update.IsCompleted ?? item.IsCompleted
            };
            updatedItems = updatedItems.Update(update.Id, updatedItem);
        }
    }
    
    return state with { Items = updatedItems };
}
```

## Integration with Store

```csharp
// Register the reducer with the store
services.AddDuckyStore(builder =>
{
    builder
        .AddSlice<TodoState>()
        .AddReducer<TodoReducer>()
        .ConfigureStore(options => options.AssemblyNames = ["MyApp"]);
});
```