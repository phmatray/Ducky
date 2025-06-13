#pragma warning disable RCS1264 // Use explicit type instead of 'var'

using Demo.ConsoleAppReactive.States;

namespace Ducky.Reactive.Tests;

public class SimpleWeatherTest
{
    [Fact]
    public void WeatherSliceReducers_Should_Have_Correct_SliceKey()
    {
        // Arrange
        var reducer = new WeatherSliceReducers();
        
        // Act
        var sliceKey = reducer.GetKey();
        var initialState = reducer.GetInitialState();
        
        // Assert
        Assert.NotNull(sliceKey);
        Assert.NotNull(initialState);
        Assert.Equal("Unknown", initialState.Location);
        
        // Debug output to see what key is generated
        Console.WriteLine($"Generated slice key: {sliceKey}");
    }

    [Fact]
    public void WeatherSliceReducers_Should_Handle_Actions_Directly()
    {
        // Arrange
        var reducer = new WeatherSliceReducers();
        var initialState = reducer.GetInitialState();
        
        // Act - StartWeatherPolling
        var state1 = reducer.Reduce(initialState, new StartWeatherPolling("Test City"));
        
        // Assert
        Assert.Equal("Test City", state1.Location);
        Assert.False(state1.IsLoading);
        Assert.Null(state1.Error);
        
        // Act - WeatherLoading
        var state2 = reducer.Reduce(state1, new WeatherLoading());
        
        // Assert
        Assert.True(state2.IsLoading);
        Assert.Equal("Test City", state2.Location);
        
        // Act - WeatherLoaded
        var state3 = reducer.Reduce(state2, new WeatherLoaded("Test City", 22.5, "Sunny"));
        
        // Assert
        Assert.False(state3.IsLoading);
        Assert.Equal("Test City", state3.Location);
        Assert.Equal(22.5, state3.Temperature);
        Assert.Equal("Sunny", state3.Condition);
        Assert.Null(state3.Error);
    }
}