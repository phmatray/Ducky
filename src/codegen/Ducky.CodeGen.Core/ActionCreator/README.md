# Action Creator Generator

The Action Creator Generator creates static factory classes for actions, providing convenient methods for creating, dispatching, and serializing actions.

## Usage

```csharp
using Ducky.CodeGen.Core;

// Configure the generator
var options = new ActionCreatorGeneratorOptions
{
    Namespace = "MyApp.Actions",
    StateType = "AppState",
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
        }
    }
};

// Generate the action creator code
var generator = new ActionCreatorGenerator();
var generatedCode = generator.GenerateCode(options);
```

## Generated Output

The generator produces static factory classes like this:

```csharp
using System;
using System.Text.Json;
using Ducky;
using MyApp.Actions;

namespace MyApp.Actions;

public static class AddTodoActionCreator
{
    public static AddTodoAction Create(int id, string title)
        => new AddTodoAction(id, title);

    public static void Dispatch(this IStore store, int id, string title)
        => store.Dispatch(Create(id, title));

    public static string AsFluxStandardAction(this AddTodoAction action)
        => JsonSerializer.Serialize(new {
            type = nameof(AddTodoAction),
            payload = new { action.Id, action.Title },
            meta = new { timestamp = DateTime.UtcNow }
        });
}

public static class ToggleTodoActionCreator
{
    public static ToggleTodoAction Create(int id)
        => new ToggleTodoAction(id);

    public static void Dispatch(this IStore store, int id)
        => store.Dispatch(Create(id));

    public static string AsFluxStandardAction(this ToggleTodoAction action)
        => JsonSerializer.Serialize(new {
            type = nameof(ToggleTodoAction),
            payload = new { action.Id },
            meta = new { timestamp = DateTime.UtcNow }
        });
}
```

## Generated Methods

Each action creator class includes three methods:

### 1. Create Method
- **Purpose**: Factory method to create action instances
- **Returns**: The action object
- **Example**: `AddTodoActionCreator.Create(1, "Learn Ducky")`

### 2. Dispatch Extension Method
- **Purpose**: Extension method on IStore for direct dispatch
- **Returns**: void
- **Example**: `store.Dispatch(1, "Learn Ducky")` (using the extension method)

### 3. AsFluxStandardAction Extension Method
- **Purpose**: Serializes action to Flux Standard Action (FSA) format
- **Returns**: JSON string
- **Format**: Includes `type`, `payload`, and `meta` properties
- **Example**: `action.AsFluxStandardAction()` for logging/debugging

## Benefits

1. **Type Safety**: All action creation is strongly typed
2. **Convenience**: Extension methods for direct dispatch
3. **Consistency**: All actions follow the same pattern
4. **Debugging**: FSA serialization for logging and debugging
5. **Reduced Boilerplate**: No need to manually write factory methods
6. **IntelliSense**: Full IDE support for action creation

## Usage in Application

```csharp
// Using the Create method
var action = AddTodoActionCreator.Create(1, "Learn Ducky");
store.Dispatch(action);

// Using the extension method (more convenient)
store.Dispatch(1, "Learn Ducky");

// For debugging/logging
var actionJson = action.AsFluxStandardAction();
logger.LogInformation("Dispatched action: {Action}", actionJson);
```