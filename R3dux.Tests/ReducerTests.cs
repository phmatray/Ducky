namespace R3dux.Tests;

public class ReducerTests
{
    private readonly CounterReducer _reducer = new();
    
    private record UnknownAction : IAction;

    [Fact]
    public void IncrementAction_Should_Increment_State_Value()
    {
        // Arrange
        const int initialState = 0;
        Increment action = new();

        // Act
        var newState = _reducer.ReduceAction(initialState, action);

        // Assert
        newState.Should().Be(1);
    }

    [Fact]
    public void DecrementAction_Should_Decrement_State_Value()
    {
        // Arrange
        const int initialState = 0;
        Decrement action = new();

        // Act
        var newState = _reducer.ReduceAction(initialState, action);

        // Assert
        newState.Should().Be(-1);
    }

    [Fact]
    public void SetValueAction_Should_Set_State_Value()
    {
        // Arrange
        const int initialState = 0;
        const int newValue = 5;
        SetValue action = new(newValue);

        // Act
        var newState = _reducer.ReduceAction(initialState, action);

        // Assert
        newState.Should().Be(newValue);
    }

    [Fact]
    public void UnknownAction_Should_Not_Change_State_Value()
    {
        // Arrange
        const int initialState = 0;
        UnknownAction action = new();

        // Act
        var newState = _reducer.ReduceAction(initialState, action);

        // Assert
        newState.Should().Be(0);
    }
}