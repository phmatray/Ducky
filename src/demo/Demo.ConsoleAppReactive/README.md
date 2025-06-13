# Ducky Console Demo

A simplified demonstration of Ducky's state management features using Spectre.Console for an interactive command-line interface.

## Features Demonstrated

This demo showcases:

1. **State Management**
   - Multiple state slices (Counter and Weather)
   - Immutable state updates using C# records
   - Type-safe actions with `[DuckyAction]` attribute

2. **Redux Pattern Implementation**
   - Actions for state mutations
   - Reducers for handling state changes
   - Store for centralized state management

3. **Interactive Console UI**
   - Beautiful console interface using Spectre.Console
   - Real-time state display
   - Menu-driven interaction

## Running the Demo

```bash
dotnet run --project src/demo/Demo.ConsoleAppReactive
```

## Architecture

### State Models

- **CounterState**: Simple numeric counter with increment/decrement/reset operations
- **WeatherState**: Weather information with location, temperature, and conditions

### Actions

- `IncrementCounter`, `DecrementCounter`, `ResetCounter`: Counter operations
- `WeatherUpdated`, `WeatherUpdateFailed`: Weather state updates

### Services

- `IWeatherService`: Interface for weather data retrieval
- `MockWeatherService`: Mock implementation generating random weather data

## Usage

1. Launch the application
2. Use the arrow keys to navigate the menu
3. Press Enter to select an action:
   - **Increment Counter**: Adds 1 to the counter
   - **Decrement Counter**: Subtracts 1 from the counter
   - **Reset Counter**: Sets counter back to 0
   - **Update Weather**: Prompts for location and fetches weather data
   - **Exit**: Closes the application

## Key Code Components

### State Definition
```csharp
public record CounterState : IState
{
    public int Count { get; init; } = 0;
}
```

### Action Definition
```csharp
[DuckyAction]
public record IncrementCounter(int Amount = 1);
```

### Reducer Implementation
```csharp
public record CounterReducers : SliceReducers<CounterState>
{
    public override CounterState GetInitialState() => new();
    
    private static CounterState Reduce(CounterState state, IncrementCounter action)
        => state with { Count = state.Count + action.Amount };
}
```

### Store Configuration
```csharp
services.AddDuckyStore(builder => builder
    .UseDefaultMiddlewares()
    .AddSlice<WeatherState>()
    .AddSlice<CounterState>());
```

## Extending the Demo

To add new features:

1. Create a new state record implementing `IState`
2. Define actions with `[DuckyAction]` attribute
3. Implement reducers extending `SliceReducers<TState>`
4. Register the slice in the store configuration
5. Add UI interactions in the main loop

## Future Enhancements

This simplified demo provides a foundation for more advanced features:

- Reactive effects for async operations
- Middleware for logging and debugging
- State persistence
- Cross-component communication
- Redux DevTools integration