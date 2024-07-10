using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

public class SliceTests
{
    private record UnknownAction : IAction;

    private readonly TestCounterSlice _sut = new();
    
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
        _sut.OnDispatch(new TestIncrementAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(11);
    }

    [Fact]
    public void CounterSlice_Should_Decrement_State()
    {
        // Act
        _sut.OnDispatch(new TestDecrementAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(9);
    }

    [Fact]
    public void CounterSlice_Should_Reset_State()
    {
        // Act
        _sut.OnDispatch(new TestIncrementAction());
        _sut.OnDispatch(new TestIncrementAction());
        _sut.OnDispatch(new TestResetAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(10);
    }

    [Fact]
    public void CounterSlice_Should_Set_State_To_Specified_Value()
    {
        // Act
        _sut.OnDispatch(new TestSetValueAction(20));
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
        _sut.OnDispatch(new TestIncrementAction());

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
}