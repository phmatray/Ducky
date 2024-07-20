// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Counter;

public sealed class CounterReducersTests : IDisposable
{
    private const string Key = "counter";
    private const int InitialState = 10;

    private readonly CounterReducers _sut = new();

    private bool _disposed;

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
    public void CounterReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(int));
    }

    [Fact]
    public void CounterReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(4);
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
