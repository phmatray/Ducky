using FluentAssertions;

namespace Demo.AppStore.Tests.Timer;

public class TimerReducersTests
{
    private readonly TimerReducers _reducers = new();

    [Fact]
    public void StartTimer_ShouldSetIsRunningToTrue()
    {
        // Arrange
        var initialState = new TimerState { Time = 0, IsRunning = false };

        // Act
        var newState = _reducers.Reduce(initialState, new StartTimer());

        // Assert
        newState.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void StopTimer_ShouldSetIsRunningToFalse()
    {
        // Arrange
        var initialState = new TimerState { Time = 0, IsRunning = true };

        // Act
        var newState = _reducers.Reduce(initialState, new StopTimer());

        // Assert
        newState.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ResetTimer_ShouldResetState()
    {
        // Arrange
        var initialState = new TimerState { Time = 5, IsRunning = true };

        // Act
        var newState = _reducers.Reduce(initialState, new ResetTimer());

        // Assert
        newState.Time.Should().Be(0);
        newState.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Tick_ShouldIncrementTimeByOne()
    {
        // Arrange
        var initialState = new TimerState { Time = 5, IsRunning = true };

        // Act
        var newState = _reducers.Reduce(initialState, new Tick());

        // Assert
        newState.Time.Should().Be(initialState.Time + 1);
    }
}