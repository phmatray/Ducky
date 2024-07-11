using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

public class ReducerCollectionTests
{
    private readonly TestCounterReducers _sut = new();

    [Fact]
    public void Map_Should_AddReducer()
    {
        // Act
        _sut.Map<IntegerAction>((state, action) => state + action.Value);

        // Assert
        _sut.Reducers.Should().ContainKey(typeof(IntegerAction));
    }

    [Fact]
    public void Reduce_Should_ApplyReducer()
    {
        // Arrange
        _sut.Map<IntegerAction>((state, action) => state + action.Value);
        var initialState = 0;
        var action = new IntegerAction(5);

        // Act
        var newState = _sut.Reduce(initialState, action);

        // Assert
        newState.Should().Be(5);
    }

    [Fact]
    public void Reduce_Should_ApplyReducer_WithInterface()
    {
        // Arrange
        _sut.Map<IntegerAction>((state, action) => state + action.Value);
        const int initialState = 0;
        IAction action = new IntegerAction(5);

        // Act
        var newState = _sut.Reduce(initialState, action);

        // Assert
        newState.Should().Be(5);
    }

    [Fact]
    public void Reduce_Should_ReturnOriginalState_IfNoReducerFound()
    {
        // Arrange
        const int initialState = 0;
        var action = new IntegerAction(5);
        
        // Act
        var newState = _sut.Reduce(initialState, action);

        // Assert
        newState.Should().Be(initialState);
    }

    [Fact]
    public void Map_Should_ThrowArgumentNullException_WhenReducerIsNull()
    {
        // Act
        Action act = () => _sut.Map<IntegerAction>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reduce_Should_ThrowArgumentNullException_WhenActionIsNull()
    {
        // Act
        Action act = () => _sut.Reduce(0, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void GetKey_Should_ReturnTypeName_Transformed()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be("test-counter");
    }
}