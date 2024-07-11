using FluentAssertions;

namespace Demo.AppStore.Tests.Timer;

public class TimerReducersTests
{
    private readonly TimerReducers _sut = new();

    private readonly TimerState _initialState = new()
    {
        Time = 0,
        IsRunning = false
    };
    
    private const string Key = "timer";

    [Fact]
    public void TimerReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }
    
    [Fact]
    public void TimerReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void StartTimer_ShouldSetIsRunningToTrue()
    {
        // Act
        var newState = _sut.Reduce(_initialState, new StartTimer());

        // Assert
        newState.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void StopTimer_ShouldSetIsRunningToFalse()
    {
        // Arrange
        var state = _initialState with { IsRunning = true };

        // Act
        var newState = _sut.Reduce(state, new StopTimer());

        // Assert
        newState.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ResetTimer_ShouldResetState()
    {
        // Arrange
        var state = new TimerState { Time = 5, IsRunning = true };

        // Act
        var newState = _sut.Reduce(state, new ResetTimer());

        // Assert
        newState.Time.Should().Be(0);
        newState.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Tick_ShouldIncrementTimeByOne()
    {
        // Arrange
        var state = new TimerState { Time = 5, IsRunning = true };

        // Act
        var newState = _sut.Reduce(state, new Tick());

        // Assert
        newState.Time.Should().Be(state.Time + 1);
    }
}