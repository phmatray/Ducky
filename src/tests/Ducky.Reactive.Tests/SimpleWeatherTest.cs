#pragma warning disable RCS1264 // Use explicit type instead of 'var'

sing Shouldly;

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
        sliceKey.ShouldNotBeNull();
        initialState.ShouldNotBeNull();
        initialState.Location.ShouldBe("Unknown");
        
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
        state1.Location.ShouldBe("Test City");
        state1.IsLoading.ShouldBeFalse();
        state1.Error.ShouldBeNull();
        
        // Act - WeatherLoading
        var state2 = reducer.Reduce(state1, new WeatherLoading());
        
        // Assert
        state2.IsLoading.ShouldBeTrue();
        state2.Location.ShouldBe("Test City");
        
        // Act - WeatherLoaded
        var state3 = reducer.Reduce(state2, new WeatherLoaded("Test City", 22.5, "Sunny"));
        
        // Assert
        state3.IsLoading.ShouldBeFalse();
        state3.Location.ShouldBe("Test City");
        state3.Temperature.ShouldBe(22.5);
        state3.Condition.ShouldBe("Sunny");
        state3.Error.ShouldBeNull();
    }
}