// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Core;

public sealed class SliceReducersTests : IDisposable
{
    private readonly TestCounterReducers _sut = new();
    private bool _disposed;

    [Fact]
    public void Map_Should_AddReducer()
    {
        // Act
        _sut.On<IntegerAction>((state, action) => state + action.Value);

        // Assert
        _sut.Reducers.Should().ContainKey(typeof(IntegerAction));
    }

    [Fact]
    public void Reduce_Should_ApplyReducer()
    {
        // Arrange
        _sut.On<IntegerAction>((state, action) => state + action.Value);
        const int initialState = 0;
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
        _sut.On<IntegerAction>((state, action) => state + action.Value);
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

    [Fact]
    public void GetState_Should_Initialize_With_Default_State()
    {
        // Act
        var state = _sut.GetState();

        // Assert
        state.Should().Be(10);
    }

    [Fact]
    public void OnDispatch_Should_Increment_State()
    {
        // Act
        _sut.OnDispatch(new TestIncrementAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(11);
    }

    [Fact]
    public void OnDispatch_Should_Decrement_State()
    {
        // Act
        _sut.OnDispatch(new TestDecrementAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(9);
    }

    [Fact]
    public void OnDispatch_Should_Reset_State()
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
    public void COnDispatch_Should_Set_State_To_Specified_Value()
    {
        // Act
        _sut.OnDispatch(new TestSetValueAction(20));
        var state = _sut.GetState();

        // Assert
        state.Should().Be(20);
    }

    [Fact]
    public void SliceReducers_Should_Emit_StateUpdated_On_State_Change()
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
    public void SliceReducers_Should_Not_Change_State_For_Unknown_Action()
    {
        // Act
        var initialState = _sut.GetState();
        _sut.OnDispatch(new UnknownAction());
        var state = _sut.GetState();

        // Assert
        state.Should().Be(initialState);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _sut.Dispose();
            }

            _disposed = true;
        }
    }
}