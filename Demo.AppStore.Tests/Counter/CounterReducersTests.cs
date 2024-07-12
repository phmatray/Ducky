using FluentAssertions;

namespace Demo.AppStore.Tests.Counter;

public class CounterReducersTests
{
    private readonly CounterReducers _sut = new();
    
    private const int InitialState = 10;
    private const string Key = "counter";

    [Fact]
    public void CounterReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().Be(InitialState);
    }
    
    [Fact]
    public void CounterReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void Increment_ShouldIncreaseStateByOne()
    {
        // Arrange
        const int initialState = 0;
        const int expectedState = 1;

        // Act
        int newState = _sut.Reduce(initialState, new Increment());

        // Assert
        newState.Should().Be(expectedState);
    }

    [Fact]
    public void Decrement_ShouldDecreaseStateByOne()
    {
        // Arrange
        const int initialState = 1;
        const int expectedState = 0;

        // Act
        int newState = _sut.Reduce(initialState, new Decrement());

        // Assert
        newState.Should().Be(expectedState);
    }

    [Fact]
    public void Reset_ShouldSetStateToInitialState()
    {
        // Arrange
        const int state = 20;

        // Act
        int newState = _sut.Reduce(state, new Reset());

        // Assert
        newState.Should().Be(InitialState);
    }

    [Fact]
    public void SetValue_ShouldSetStateToGivenValue()
    {
        // Arrange
        const int valueToSet = 42;

        // Act
        int newState = _sut.Reduce(InitialState, new SetValue(valueToSet));

        // Assert
        newState.Should().Be(valueToSet);
    }
}