using FluentAssertions;

namespace Demo.AppStore.Tests.Counter;

public class CounterReducersTests
{
    private readonly CounterReducers _reducers = new();
    private const int InitialState = 10;

    [Fact]
    public void Increment_ShouldIncreaseStateByOne()
    {
        // Arrange
        const int initialState = 0;

        // Act
        int newState = _reducers.Reduce(initialState, new Increment());

        // Assert
        newState.Should().Be(initialState + 1);
    }

    [Fact]
    public void Decrement_ShouldDecreaseStateByOne()
    {
        // Arrange
        const int initialState = 1;

        // Act
        int newState = _reducers.Reduce(initialState, new Decrement());

        // Assert
        newState.Should().Be(initialState - 1);
    }

    [Fact]
    public void Reset_ShouldSetStateToInitialState()
    {
        // Arrange
        const int initialState = 20;

        // Act
        int newState = _reducers.Reduce(initialState, new Reset());

        // Assert
        newState.Should().Be(InitialState);
    }

    [Fact]
    public void SetValue_ShouldSetStateToGivenValue()
    {
        // Arrange
        const int initialState = 0;
        const int valueToSet = 42;

        // Act
        int newState = _reducers.Reduce(initialState, new SetValue(valueToSet));

        // Assert
        newState.Should().Be(valueToSet);
    }
}