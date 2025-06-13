#pragma warning disable RCS1264 // Use explicit type instead of 'var'

using Demo.ConsoleAppReactive.States;
using Ducky;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ducky.Reactive.Tests;

public class WeatherStateTests
{
    [Fact]
    public void WeatherSliceReducers_Should_Handle_StartWeatherPolling()
    {
        // Arrange
        var reducer = new WeatherSliceReducers();
        var initialState = reducer.GetInitialState();
        var action = new StartWeatherPolling("New York");

        // Act
        var newState = reducer.Reduce(initialState, action);

        // Assert
        Assert.Equal("New York", newState.Location);
        Assert.False(newState.IsLoading);
        Assert.Null(newState.Error);
    }

    [Fact]
    public void WeatherSliceReducers_Should_Handle_WeatherLoading()
    {
        // Arrange
        var reducer = new WeatherSliceReducers();
        var initialState = new WeatherState { Location = "New York" };
        var action = new WeatherLoading();

        // Act
        var newState = reducer.Reduce(initialState, action);

        // Assert
        Assert.True(newState.IsLoading);
        Assert.Null(newState.Error);
        Assert.Equal("New York", newState.Location);
    }

    [Fact]
    public void WeatherSliceReducers_Should_Handle_WeatherLoaded()
    {
        // Arrange
        var reducer = new WeatherSliceReducers();
        var initialState = new WeatherState { Location = "New York", IsLoading = true };
        var action = new WeatherLoaded("New York", 25.5, "Sunny");

        // Act
        var newState = reducer.Reduce(initialState, action);

        // Assert
        Assert.False(newState.IsLoading);
        Assert.Null(newState.Error);
        Assert.Equal("New York", newState.Location);
        Assert.Equal(25.5, newState.Temperature);
        Assert.Equal("Sunny", newState.Condition);
    }

    [Fact]
    public void WeatherSliceReducers_Should_Handle_WeatherError()
    {
        // Arrange
        var reducer = new WeatherSliceReducers();
        var initialState = new WeatherState { Location = "New York", IsLoading = true };
        var action = new WeatherError("Network error");

        // Act
        var newState = reducer.Reduce(initialState, action);

        // Assert
        Assert.False(newState.IsLoading);
        Assert.Equal("Network error", newState.Error);
        Assert.Equal("New York", newState.Location);
    }

    [Fact]
    public void Store_Integration_Should_Handle_Weather_Actions()
    {
        // Arrange - Use DuckyStore services but register slice manually
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Register the concrete slice implementation manually - both interfaces
        services.AddScoped<WeatherSliceReducers>();
        services.AddScoped<ISlice<WeatherState>>(sp => sp.GetRequiredService<WeatherSliceReducers>());
        services.AddScoped<ISlice>(sp => sp.GetRequiredService<WeatherSliceReducers>());
        
        // Use standard Ducky services but avoid the broken AddSlice<T>() method
        services.AddDucky();

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        var store = serviceProvider.GetRequiredService<IStore>();

        // Act & Assert - Start Weather Polling
        dispatcher.Dispatch(new StartWeatherPolling("Test City"));
        var state1 = store.CurrentState.GetSliceState<WeatherState>();
        Assert.Equal("Test City", state1.Location);
        Assert.False(state1.IsLoading);

        // Act & Assert - Weather Loading
        dispatcher.Dispatch(new WeatherLoading());
        var state2 = store.CurrentState.GetSliceState<WeatherState>();
        Assert.True(state2.IsLoading);
        Assert.Equal("Test City", state2.Location);

        // Act & Assert - Weather Loaded
        dispatcher.Dispatch(new WeatherLoaded("Test City", 20.0, "Cloudy"));
        var state3 = store.CurrentState.GetSliceState<WeatherState>();
        Assert.False(state3.IsLoading);
        Assert.Equal("Test City", state3.Location);
        Assert.Equal(20.0, state3.Temperature);
        Assert.Equal("Cloudy", state3.Condition);
        Assert.Null(state3.Error);

        // Act & Assert - Weather Error
        dispatcher.Dispatch(new WeatherError("Test error"));
        var state4 = store.CurrentState.GetSliceState<WeatherState>();
        Assert.False(state4.IsLoading);
        Assert.Equal("Test error", state4.Error);
    }
}