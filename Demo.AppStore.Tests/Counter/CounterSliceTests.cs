using FluentAssertions;

namespace Demo.AppStore.Tests.Counter;

public class CounterSliceTests
{
    private readonly CounterSlice _sut = new();

    [Fact]
    public void CounterSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(int));
    }

    [Fact]
    public void CounterSlice_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Reducers.Should().HaveCount(4);
    }
}