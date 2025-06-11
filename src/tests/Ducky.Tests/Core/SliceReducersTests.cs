// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

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
        _sut.Reducers.ShouldContainKey(typeof(IntegerAction));
    }

    [Fact]
    public void Reduce_Should_ApplyReducer()
    {
        // Arrange
        _sut.On<IntegerAction>((state, action) => state + action.Value);
        const int initialState = 0;
        IntegerAction action = new(5);

        // Act
        int newState = _sut.Reduce(initialState, action);

        // Assert
        newState.ShouldBe(5);
    }

    [Fact]
    public void Reduce_Should_ApplyReducer_WithObject()
    {
        // Arrange
        _sut.On<IntegerAction>((state, action) => state + action.Value);
        const int initialState = 0;
        object action = new IntegerAction(5);

        // Act
        int newState = _sut.Reduce(initialState, action);

        // Assert
        newState.ShouldBe(5);
    }

    [Fact]
    public void Reduce_Should_ReturnOriginalState_IfNoReducerFound()
    {
        // Arrange
        const int initialState = 0;
        IntegerAction action = new(5);

        // Act
        int newState = _sut.Reduce(initialState, action);

        // Assert
        newState.ShouldBe(initialState);
    }

    [Fact]
    public void Reduce_Should_ThrowArgumentNullException_WhenActionIsNull()
    {
        // Act
        Action act = () => _sut.Reduce(0, null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void GetKey_Should_ReturnTypeName_Transformed()
    {
        // Act
        string key = _sut.GetKey();

        // Assert - Now includes namespace for robustness
        key.ShouldBe("ducky-tests-test-models-test-counter");
    }

    [Fact]
    public void GetState_Should_Initialize_With_Default_State()
    {
        // Act
        object state = _sut.GetState();

        // Assert
        state.ShouldBe(10);
    }

    [Fact]
    public void GetJson_Should_Return_Json_Object()
    {
        // Act
        JsonObject json = _sut.GetJson();

        // Assert
        json.ShouldNotBeNull();
        json.ContainsKey("key").ShouldBeTrue();
        json.ContainsKey("state").ShouldBeTrue();
        json["key"]!.GetValue<string>().ShouldBe("ducky-tests-test-models-test-counter");
        json["state"]!.GetValue<int>().ShouldBe(10);
    }

    [Fact]
    public void OnDispatch_Should_Increment_State()
    {
        // Act
        _sut.OnDispatch(new TestIncrementAction());
        object state = _sut.GetState();

        // Assert
        state.ShouldBe(11);
    }

    [Fact]
    public void OnDispatch_Should_Decrement_State()
    {
        // Act
        _sut.OnDispatch(new TestDecrementAction());
        object state = _sut.GetState();

        // Assert
        state.ShouldBe(9);
    }

    [Fact]
    public void OnDispatch_Should_Reset_State()
    {
        // Act
        _sut.OnDispatch(new TestIncrementAction());
        _sut.OnDispatch(new TestIncrementAction());
        _sut.OnDispatch(new TestResetAction());
        object state = _sut.GetState();

        // Assert
        state.ShouldBe(10);
    }

    [Fact]
    public void COnDispatch_Should_Set_State_To_Specified_Value()
    {
        // Act
        _sut.OnDispatch(new TestSetValueAction(20));
        object state = _sut.GetState();

        // Assert
        state.ShouldBe(20);
    }

    [Fact]
    public void SliceReducers_Should_Emit_StateUpdated_On_State_Change()
    {
        // Arrange
        var stateUpdatedEmitted = false;
        _sut.StateUpdated += (sender, args) => stateUpdatedEmitted = true;

        // Act
        _sut.OnDispatch(new TestIncrementAction());

        // Assert
        stateUpdatedEmitted.ShouldBeTrue();
    }

    [Fact]
    public void SliceReducers_Should_Not_Change_State_For_Unknown_Action()
    {
        // Act
        object initialState = _sut.GetState();
        _sut.OnDispatch(new UnknownAction());
        object state = _sut.GetState();

        // Assert
        state.ShouldBe(initialState);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
