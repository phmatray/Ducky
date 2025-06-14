#pragma warning disable RCS1264 // Use explicit type instead of 'var'

using Demo.ConsoleAppReactive.States;
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
        newState.Location.ShouldBe("New York");
        newState.IsLoading.ShouldBeFalse();
        newState.Error.ShouldBeNull();
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
        newState.IsLoading.ShouldBeTrue();
        newState.Error.ShouldBeNull();
        newState.Location.ShouldBe("New York");
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
        newState.IsLoading.ShouldBeFalse();
        newState.Error.ShouldBeNull();
        newState.Location.ShouldBe("New York");
        newState.Temperature.ShouldBe(25.5);
        newState.Condition.ShouldBe("Sunny");
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
        newState.IsLoading.ShouldBeFalse();
        newState.Error.ShouldBe("Network error");
        newState.Location.ShouldBe("New York");
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
        var state1 = store.GetSlice<WeatherState>();
        state1.Location.ShouldBe("Test City");
        state1.IsLoading.ShouldBeFalse();

        // Act & Assert - Weather Loading
        dispatcher.Dispatch(new WeatherLoading());
        var state2 = store.GetSlice<WeatherState>();
        state2.IsLoading.ShouldBeTrue();
        state2.Location.ShouldBe("Test City");

        // Act & Assert - Weather Loaded
        dispatcher.Dispatch(new WeatherLoaded("Test City", 20.0, "Cloudy"));
        var state3 = store.GetSlice<WeatherState>();
        state3.IsLoading.ShouldBeFalse();
        state3.Location.ShouldBe("Test City");
        state3.Temperature.ShouldBe(20.0);
        state3.Condition.ShouldBe("Cloudy");
        state3.Error.ShouldBeNull();

        // Act & Assert - Weather Error
        dispatcher.Dispatch(new WeatherError("Test error"));
        var state4 = store.GetSlice<WeatherState>();
        state4.IsLoading.ShouldBeFalse();
        state4.Error.ShouldBe("Test error");
    }
}