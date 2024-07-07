using R3dux.Temp;

namespace R3dux.Tests;

public class ReducerTests
{
    private const int InitialState = 0;
    private readonly CounterReducers _reducer = new();
    
    private record UnknownAction : IAction;

    [Fact]
    public void IncrementAction_Should_Increment_State_Value()
    {
        // Arrange
        Increment action = new();

        // Act
        var newState = _reducer.Reduce(InitialState, action);

        // Assert
        newState.Should().Be(1);
    }

    [Fact]
    public void DecrementAction_Should_Decrement_State_Value()
    {
        // Arrange
        Decrement action = new();

        // Act
        var newState = _reducer.Reduce(InitialState, action);

        // Assert
        newState.Should().Be(-1);
    }

    [Fact]
    public void SetValueAction_Should_Set_State_Value()
    {
        // Arrange
        const int newValue = 5;
        SetValue action = new(newValue);

        // Act
        var newState = _reducer.Reduce(InitialState, action);

        // Assert
        newState.Should().Be(newValue);
    }

    [Fact]
    public void UnknownAction_Should_Not_Change_State_Value()
    {
        // Arrange
        UnknownAction action = new();

        // Act
        var newState = _reducer.Reduce(InitialState, action);

        // Assert
        newState.Should().Be(0);
    }
}