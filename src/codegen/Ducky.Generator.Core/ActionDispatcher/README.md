# Action Dispatcher Generator

The Action Dispatcher Generator creates extension methods for dispatching actions directly on `IDispatcher` instances with proper null checks and documentation.

## Usage

```csharp
using Ducky.CodeGen.Core;

// Configure the generator
var options = new ActionDispatcherGeneratorOptions
{
    ActionName = "AddTodo",
    ActionFullyQualifiedName = "MyApp.Actions.AddTodoAction",
    Parameters = new List<ParameterDescriptor>
    {
        new ParameterDescriptor { ParamName = "id", ParamType = "int" },
        new ParameterDescriptor { ParamName = "title", ParamType = "string" }
    }
};

// Generate the dispatcher code
var generator = new ActionDispatcherGenerator();
var generatedCode = generator.GenerateCode(options);
```

## Generated Output

The generator produces extension methods like this:

```csharp
using System;

public static partial class ActionDispatcher
{
    /// <summary>
    /// Dispatches a new AddTodo action.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="id">The id parameter.</param>
    /// <param name="title">The title parameter.</param>
    public static void AddTodo(this Ducky.IDispatcher dispatcher, int id, string title)
    {
        if (dispatcher is null)
        {
            throw new System.ArgumentNullException(nameof(dispatcher));
        }

        dispatcher.Dispatch(new MyApp.Actions.AddTodoAction(id, title));
    }
}
```

## Features

### 1. Extension Methods
- Creates extension methods on `IDispatcher` interface
- Methods are named after the action (e.g., `AddTodo` for `AddTodoAction`)
- Strongly typed parameters matching the action constructor

### 2. Null Safety
- Automatic null checks on the dispatcher parameter
- Throws `ArgumentNullException` with proper parameter name
- Prevents runtime errors from null dispatchers

### 3. XML Documentation
- Generates comprehensive XML documentation
- Includes summary and parameter descriptions
- Provides IntelliSense support in IDEs

### 4. Partial Class Design
- Uses partial classes for extensibility
- Multiple generators can contribute to the same class
- Avoids conflicts between different action dispatchers

## Benefits

1. **Convenience**: Direct dispatch without creating action instances
2. **Type Safety**: Strongly typed parameters prevent errors
3. **Null Safety**: Built-in null checks prevent runtime exceptions
4. **Documentation**: Auto-generated XML docs for better developer experience
5. **Consistency**: All dispatchers follow the same pattern
6. **Extensibility**: Partial class design allows combining multiple dispatchers

## Usage in Application

```csharp
// Inject or obtain the dispatcher
IDispatcher dispatcher = serviceProvider.GetService<IDispatcher>();

// Use the generated extension method
dispatcher.AddTodo(1, "Learn Ducky");

// The extension method handles:
// - Null checking the dispatcher
// - Creating the action instance
// - Dispatching the action
```

## Integration with DI

```csharp
// In your service registration
services.AddScoped<IDispatcher, MyDispatcher>();

// In your component/service
public class TodoService
{
    private readonly IDispatcher _dispatcher;
    
    public TodoService(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }
    
    public void AddNewTodo(string title)
    {
        var id = GenerateId();
        _dispatcher.AddTodo(id, title); // Uses generated extension method
    }
}
```