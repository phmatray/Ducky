namespace R3dux.Tests.Core;

public class ReducerCollectionTests
{
    private record TestState(int Value);
    private record IncrementAction(int Amount) : IAction;
    private record DecrementAction(int Amount) : IAction;
    
    private readonly ReducerCollection<TestState> _sut = new();

    [Fact]
    public void Map_Should_AddReducer()
    {
        // Act
        _sut.Map<IncrementAction>((state, action) => new TestState(state.Value + action.Amount));

        // Assert
        _sut.Reducers.Should().ContainKey(typeof(IncrementAction));
    }

    [Fact]
    public void Reduce_Should_ApplyReducer()
    {
        // Arrange
        _sut.Map<IncrementAction>((state, action) => new TestState(state.Value + action.Amount));
        var initialState = new TestState(0);
        var action = new IncrementAction(5);

        // Act
        var newState = _sut.Reduce(initialState, action);

        // Assert
        newState.Value.Should().Be(5);
    }

    [Fact]
    public void Reduce_Should_ApplyReducer_WithInterface()
    {
        // Arrange
        _sut.Map<IncrementAction>((state, action) => new TestState(state.Value + action.Amount));
        var initialState = new TestState(0);
        IAction action = new IncrementAction(5);

        // Act
        var newState = _sut.Reduce(initialState, action);

        // Assert
        newState.Value.Should().Be(5);
    }

    [Fact]
    public void Reduce_Should_ReturnOriginalState_IfNoReducerFound()
    {
        // Arrange
        var initialState = new TestState(0);
        var action = new DecrementAction(5);
        
        // Act
        var newState = _sut.Reduce(initialState, action);

        // Assert
        newState.Should().Be(initialState);
    }

    [Fact]
    public void Map_Should_ThrowArgumentNullException_WhenReducerIsNull()
    {
        // Act
        Action act = () => _sut.Map<IncrementAction>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reduce_Should_ThrowArgumentNullException_WhenActionIsNull()
    {
        // Act
        Action act = () => _sut.Reduce(new TestState(0), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}