using FluentAssertions;

namespace Demo.AppStore.Tests.Timer;

public class TimerSliceTests
{
    private readonly TimerSlice _sut = new();
    
    [Fact]
    public void TimerSlice_Should_Return_Correct_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be("timer");
    }

    [Fact]
    public void TimerSlice_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(new TimerState
        {
            Time = 0,
            IsRunning = false
        });
    }

    [Fact]
    public void TimerSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(TimerState));
    }
    
    [Fact]
    public void TimerSlice_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Reducers.Should().HaveCount(4);
    }
}