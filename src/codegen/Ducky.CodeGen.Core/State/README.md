# State Generator

The State Generator creates immutable state classes with record types, supporting normalized state patterns and proper inheritance hierarchies.

## Usage

```csharp
using Ducky.CodeGen.Core;

// Configure the generator
var options = new StateGeneratorOptions
{
    Namespace = "MyApp.State",
    States = new List<StateDescriptor>
    {
        new StateDescriptor
        {
            StateName = "TodoState",
            BaseClass = "IState",
            Properties = new List<PropertyDescriptor>
            {
                new PropertyDescriptor 
                { 
                    PropertyName = "Items", 
                    PropertyType = "NormalizedState<int, TodoItem>",
                    DefaultValue = "new()"
                },
                new PropertyDescriptor 
                { 
                    PropertyName = "IsLoading", 
                    PropertyType = "bool",
                    DefaultValue = "false"
                },
                new PropertyDescriptor 
                { 
                    PropertyName = "Error", 
                    PropertyType = "string?",
                    DefaultValue = "null"
                }
            }
        },
        new StateDescriptor
        {
            StateName = "AppState",
            Properties = new List<PropertyDescriptor>
            {
                new PropertyDescriptor 
                { 
                    PropertyName = "Todos", 
                    PropertyType = "TodoState",
                    DefaultValue = "new()"
                },
                new PropertyDescriptor 
                { 
                    PropertyName = "User", 
                    PropertyType = "UserState",
                    DefaultValue = "new()"
                }
            }
        }
    }
};

// Generate the state code
var generator = new StateGenerator();
var generatedCode = generator.GenerateCode(options);
```

## Generated Output

The generator produces immutable state records like this:

```csharp
using System;
using Ducky;
using MyApp.State;

namespace MyApp.State;

public record TodoState : IState
{
    public NormalizedState<int, TodoItem> Items { get; init; } = new();
    public bool IsLoading { get; init; } = false;
    public string? Error { get; init; } = null;
}

public record AppState
{
    public TodoState Todos { get; init; } = new();
    public UserState User { get; init; } = new();
}
```

## Features

### 1. Immutable Records
- Uses C# record types for immutability
- Properties use `init` accessors for initialization
- Supports `with` expressions for updates

### 2. Default Values
- Automatic default value assignment
- Supports complex initialization expressions
- Ensures states are always properly initialized

### 3. Inheritance Support
- Configurable base classes (e.g., `IState`)
- Maintains proper inheritance hierarchies
- Supports interface implementations

### 4. Normalized State Support
- Built-in support for `NormalizedState<TKey, TEntity>` patterns
- Optimized for managing collections and relationships
- Reduces state duplication and improves performance

## Property Types

The generator supports various property types:

### Primitive Types
```csharp
public bool IsLoading { get; init; } = false;
public int Count { get; init; } = 0;
public string? Message { get; init; } = null;
```

### Complex Types
```csharp
public TodoState Todos { get; init; } = new();
public List<string> Tags { get; init; } = new();
public Dictionary<string, object> Metadata { get; init; } = new();
```

### Normalized State
```csharp
public NormalizedState<int, TodoItem> Items { get; init; } = new();
public NormalizedState<Guid, User> Users { get; init; } = new();
```

## Benefits

1. **Immutability**: Records ensure state cannot be accidentally mutated
2. **Type Safety**: Strongly typed properties prevent runtime errors
3. **Performance**: Normalized state reduces duplication and improves lookup performance
4. **IntelliSense**: Full IDE support for state properties
5. **Debugging**: Record types provide excellent debugging experience
6. **Consistency**: All states follow the same patterns

## Usage with Reducers

```csharp
// State updates using 'with' expressions
public static TodoState AddTodo(TodoState state, AddTodoAction action)
{
    var newItem = new TodoItem(action.Id, action.Title);
    return state with 
    { 
        Items = state.Items.Add(action.Id, newItem),
        IsLoading = false,
        Error = null
    };
}

// Nested state updates
public static AppState UpdateTodos(AppState state, TodoState newTodos)
{
    return state with { Todos = newTodos };
}
```

## Normalized State Patterns

```csharp
// Working with normalized state
public record TodoState : IState
{
    public NormalizedState<int, TodoItem> Items { get; init; } = new();
    public List<int> SelectedIds { get; init; } = new();
}

// Reducer using normalized state
public static TodoState SelectTodo(TodoState state, SelectTodoAction action)
{
    return state with 
    { 
        SelectedIds = state.SelectedIds.Contains(action.Id)
            ? state.SelectedIds.Where(id => id != action.Id).ToList()
            : state.SelectedIds.Append(action.Id).ToList()
    };
}
```