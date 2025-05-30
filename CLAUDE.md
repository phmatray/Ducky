# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Development Commands

### Build Commands
```bash
# Build the entire solution
dotnet build

# Build with NUKE (recommended)
./build.ps1          # Windows
./build.sh           # macOS/Linux

# NUKE targets
./build.sh Clean     # Clean solution and artifacts
./build.sh Compile   # Build the solution
./build.sh UnitTests # Run unit tests
./build.sh Pack      # Create NuGet packages
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test src/tests/Ducky.Tests
dotnet test src/tests/AppStore.Tests
dotnet test src/tests/Demo.BlazorWasm.E2E.Tests

# Run tests with filter
dotnet test --filter "FullyQualifiedName~StoreBuilder"
dotnet test --filter "FullyQualifiedName~Middleware"

# Run single test
dotnet test --filter "MethodName~SpecificTestName"
```

### Running Demo Applications
```bash
# Blazor WebAssembly demo (main example)
dotnet run --project src/demo/Demo.BlazorWasm

# Console app demo
dotnet run --project src/demo/Demo.ConsoleApp
```

### Package Management
```bash
# Restore packages
dotnet restore

# Pack for local testing
dotnet pack

# Run E2E tests (requires Playwright)
dotnet test src/tests/Demo.BlazorWasm.E2E.Tests
```

## High-Level Architecture

### Core State Management (Redux Pattern)
Ducky implements a Redux-inspired unidirectional data flow pattern with strong typing:

1. **Store** (`DuckyStore`): Single source of truth containing application state
2. **Actions**: Plain objects/records describing state changes  
3. **Reducers** (`SliceReducers<TState>`): Pure functions that calculate new state
4. **Selectors**: Query and derive data from state with memoization support
5. **Effects**: Handle side effects (async operations, API calls)

### Middleware Pipeline Architecture
Actions flow through a configurable middleware pipeline using reactive streams:
- **IActionMiddleware**: Interface for middleware that can intercept actions before/after reducer
- **Pipeline Flow**: Actions → Middleware (Before) → Reducer → Middleware (After) → Effects
- **Built-in Middleware**: CorrelationId, ExceptionHandling, AsyncEffect, ReactiveEffect, NoOp
- **Reactive Design**: Uses R3 observables for reactive middleware and effects

### Store Configuration Patterns

#### New StoreBuilder Pattern (Recommended)
Robust configuration with proper dependency injection:
```csharp
services.AddDuckyStore(builder =>
{
    builder
        .AddDefaultMiddlewares()  // Correlation, Exception, Async/Reactive Effects
        .AddSlice<CounterState>()
        .AddEffect<MyApiEffect>()
        .AddExceptionHandler<MyErrorHandler>()
        .ConfigureStore(options => options.AssemblyNames = ["MyApp"]);
});
```

#### Legacy Configuration Methods
Also available for backward compatibility:
- `AddDucky()` - Basic setup
- `AddDuckyWithMiddleware(types)` - With specific middleware types
- `AddDucky(configure)` - With fluent configuration

### Project Structure
- `Ducky/`: Core library with state management primitives
- `Ducky.Blazor/`: Blazor-specific integrations (components, DevTools, persistence, cross-tab sync)
- `Ducky.Generator/`: Source generators to reduce boilerplate
- `Demo.BlazorWasm/`: Comprehensive example Blazor WebAssembly application
- `Demo.ConsoleApp/`: Simple console application example

### Key Design Patterns

#### Duck Pattern
Related actions, reducers, and effects are grouped in single files (e.g., `CounterDucks.cs`):
```csharp
public static class CounterDucks
{
    // Actions
    public record Increment(int Amount = 1);
    public record Reset;
    
    // Reducers
    public static SliceReducers<CounterState> Reducers = new(new CounterState())
        .On<Increment>((state, action) => state with { Count = state.Count + action.Amount })
        .On<Reset>((state, _) => new CounterState());
}
```

#### Immutable State Updates
Use C# records and `with` expressions for immutability:
```csharp
state with { Count = state.Count + 1 }
```

#### Normalized State
For collections, use `NormalizedState<TKey, TEntity>` to maintain relationships:
```csharp
public record TodoState(NormalizedState<Guid, TodoItem> Items);
```

#### Reactive Effects
Handle side effects reactively using R3 observables:
```csharp
public class TimerEffect : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<StartTimer>()
            .SwitchSelect(_ => Observable.Interval(TimeSpan.FromSeconds(1), TimeProvider)
                .Select(_ => new Tick())
                .TakeUntil(actions.OfActionType<StopTimer>()));
    }
}
```

### Important Implementation Notes

#### Middleware Registration
When creating custom middleware extension methods, always register both concrete type and interface:
```csharp
services.TryAddScoped<MyMiddleware>();
services.AddScoped<IActionMiddleware>(sp => sp.GetRequiredService<MyMiddleware>());
```
Use `AddScoped` (not `TryAddScoped`) for IActionMiddleware to allow multiple implementations.

#### Testing with Time
For reactive effects using timers, advance time incrementally in tests:
```csharp
// Instead of: _timeProvider.Advance(TimeSpan.FromSeconds(3));
for (int i = 0; i < 3; i++)
{
    _timeProvider.Advance(TimeSpan.FromSeconds(1));
}
```

#### State Slices
SliceReducers<TState> is abstract - users must create concrete implementations. The StoreBuilder handles basic registration but complex scenarios need custom implementations.

### Blazor Integration
- **DuckyComponent<TState>**: Base component for automatic re-rendering on state changes
- **Redux DevTools**: Support via DevToolsMiddleware for browser debugging
- **Cross-Tab Sync**: Synchronize state across browser tabs via CrossTabSync
- **Persistence**: Local storage persistence via PersistenceMiddleware
- **DuckyErrorBoundary**: Error handling component for Blazor applications

### Technology Stack
- **.NET 9.0**: Latest .NET version
- **C# 13**: Latest language features
- **R3**: Reactive extensions for .NET (reactive streams)
- **Blazor WebAssembly**: For web demos and components
- **xUnit**: Unit testing framework
- **Playwright**: E2E testing framework
- **NUKE**: Build automation system
- **Source Generators**: Reduce boilerplate code

## Code Generation Infrastructure

Ducky includes both source generators for compile-time code generation and standalone generators for development-time code generation.

### Source Generators (Compile-Time)
Located in `src/library/Ducky.Generator/`:
- **ActionDispatcherSourceGenerator**: Automatically creates extension methods for actions marked with `[DuckyAction]`
- Uses incremental generation for performance
- Integrates seamlessly with IDE and build process

### Standalone Generators (Development-Time)
Located in `src/codegen/`:
- **Ducky.CodeGen.Core**: Core generation infrastructure with visitor pattern
- **Ducky.CodeGen.WebApp**: Blazor UI for interactive code generation
- **Ducky.CodeGen.Cli**: Command-line interface for scripted generation

#### Available Generators
1. **ActionCreatorGenerator**: Creates static factory classes for actions
2. **ReducerGenerator**: Generates reducer classes with partial method signatures for custom logic

#### Generator Architecture
The codegen infrastructure uses a visitor pattern with strongly-typed model classes:
```csharp
// 1. Build model
var model = new CompilationUnitElement 
{
    Usings = ["System", "Ducky"],
    Namespaces = [new NamespaceElement { Name = "MyApp.Actions", Classes = [...] }]
};

// 2. Generate syntax tree via visitor
var visitor = new SyntaxFactoryVisitor();
var syntaxNode = model.Accept(visitor);

// 3. Format and render
var formatted = Format(syntaxNode);
return formatted.ToFullString();
```

#### Running Code Generators
```bash
# Web UI (interactive)
dotnet run --project src/codegen/Ducky.CodeGen.WebApp

# CLI (scriptable)
dotnet run --project src/codegen/Ducky.CodeGen.Cli

# Build generators
dotnet build src/codegen/Ducky.CodeGen.Core
```

## Redux DevTools Integration

Ducky includes seamless integration with the Redux DevTools browser extension for powerful debugging capabilities.

### Setup

1. **Install the Redux DevTools browser extension**:
   - [Chrome Extension](https://chrome.google.com/webstore/detail/redux-devtools/lmhkpmbekcpmknklioeibfkpmmfibljd)
   - [Firefox Extension](https://addons.mozilla.org/en-US/firefox/addon/reduxdevtools/)

2. **Add DevTools middleware to your application**:
```csharp
// Basic setup
services.AddDevToolsMiddleware("MyAppStore");

// Advanced configuration
services.AddDevToolsMiddleware(options =>
{
    options.StoreName = "MyAppStore";
    options.Enabled = environment.IsDevelopment(); // Only in development
    options.ExcludedActionTypes = ["Tick", "Heartbeat"]; // Filter noisy actions
    options.MaxAge = 50; // Limit action history
    options.EnableTimeTravel = true; // Allow state time-travel
});
```

3. **Add to middleware pipeline**:
```csharp
services.AddDuckyWithPipeline(configuration, (pipeline, serviceProvider) =>
{
    pipeline.Use(serviceProvider.GetRequiredService<CorrelationIdMiddleware>());
    pipeline.Use(serviceProvider.GetRequiredService<ExceptionHandlingMiddleware>());
    pipeline.Use(serviceProvider.GetRequiredService<DevToolsMiddleware>()); // Add DevTools
});
```

4. **Initialize DevTools in your Blazor app**:
```csharp
// In Program.cs or App.razor
@inject ReduxDevToolsModule DevTools

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await DevTools.InitAsync();
    }
}
```

### Features

#### Action Monitoring
- **Real-time action tracking**: See every action dispatched in your application
- **Action payload inspection**: Examine action data and parameters
- **Action filtering**: Exclude noisy actions like timer ticks
- **Action search**: Find specific actions in the history

#### State Inspection
- **Current state view**: Browse the complete application state tree
- **State diff visualization**: See exactly what changed between states
- **State tree navigation**: Drill down into nested state objects
- **JSON export**: Export state snapshots for analysis

#### Time-Travel Debugging
- **Jump to action**: Navigate to any point in the action history
- **Skip actions**: Temporarily ignore specific actions
- **Reset state**: Return to initial application state
- **Replay actions**: Re-run actions from any point

#### Development Tools
- **Stack traces**: See where actions were dispatched from
- **Performance monitoring**: Track action processing times
- **Error handling**: DevTools gracefully handles errors without breaking your app
- **Development-only**: Automatically disabled in production builds

### Configuration Options

```csharp
public class DevToolsOptions
{
    // Store identification
    public string StoreName { get; set; } = "DuckyStore";
    
    // Enable/disable (typically disabled in production)
    public bool Enabled { get; set; } = true;
    
    // Performance settings
    public bool Realtime { get; set; } = true;
    public int MaxAge { get; set; } = 50;
    
    // Debugging features
    public bool Trace { get; set; } = true;
    public int TraceLimit { get; set; } = 25;
    public bool EnableTimeTravel { get; set; } = true;
    
    // Action filtering
    public string[] ExcludedActionTypes { get; set; } = [];
    public Func<object, bool>? ShouldLogAction { get; set; }
    
    // UI preferences
    public bool CollapseActions { get; set; } = false;
}
```

### Best Practices

1. **Development Only**: Always disable DevTools in production
2. **Filter Noisy Actions**: Exclude high-frequency actions like timers
3. **Limit History**: Set reasonable MaxAge to prevent memory issues
4. **Meaningful Action Names**: Use descriptive action type names
5. **State Structure**: Keep state serializable for DevTools compatibility

### Troubleshooting

**DevTools not appearing:**
- Ensure the browser extension is installed and enabled
- Check that `Enabled = true` in development
- Verify middleware is added to the pipeline
- Look for console errors in browser developer tools

**Performance issues:**
- Reduce `MaxAge` to limit stored actions
- Exclude high-frequency actions via `ExcludedActionTypes`
- Disable `Trace` if not needed
- Consider disabling `Realtime` for very busy applications

**Time-travel not working:**
- Ensure `EnableTimeTravel = true`
- Verify state is fully serializable
- Check for circular references in state objects
- Note: Time-travel requires implementing state restoration (currently logged only)