namespace R3dux.Tests.Core;

public class SliceTests
{
    private record UnknownAction : IAction;

    private readonly CounterSlice _sut = new();
    
    [Fact]
    public void CounterSlice_Should_Initialize_With_Default_State()
    {
        // Act
        var state = _sut.GetState();

        // Assert
        state.Should().Be(10);
    }

    [Fact]
    public void CounterSlice_Should_Increment_State()
    {
        // Act
        _sut.OnDispatch(new Increment());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(11);
    }

    [Fact]
    public void CounterSlice_Should_Decrement_State()
    {
        // Act
        _sut.OnDispatch(new Decrement());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(9);
    }

    [Fact]
    public void CounterSlice_Should_Reset_State()
    {
        // Act
        _sut.OnDispatch(new Increment());
        _sut.OnDispatch(new Increment());
        _sut.OnDispatch(new Reset());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(10);
    }

    [Fact]
    public void CounterSlice_Should_Set_State_To_Specified_Value()
    {
        // Act
        _sut.OnDispatch(new SetValue(20));
        var state = _sut.GetState();

        // Assert
        state.Should().Be(20);
    }
    
    
    [Fact]
    public void CounterSlice_Should_Emit_StateUpdated_On_State_Change()
    {
        // Arrange
        var stateUpdatedEmitted = false;
        _sut.StateUpdated.Subscribe(_ => stateUpdatedEmitted = true);

        // Act
        _sut.OnDispatch(new Increment());

        // Assert
        stateUpdatedEmitted.Should().BeTrue();
    }

    [Fact]
    public void CounterSlice_Should_Not_Change_State_For_Unknown_Action()
    {
        
        // Act
        var initialState = _sut.GetState();
        _sut.OnDispatch(new UnknownAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(initialState);
    }

    [Fact]
    public void CounterSlice_Should_Return_Correct_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be("counter");
    }

    [Fact]
    public void CounterSlice_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().Be(10);
    }

    [Fact]
    public void CounterSlice_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(int));
    }
}