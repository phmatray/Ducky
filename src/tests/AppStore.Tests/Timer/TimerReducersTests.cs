// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Timer;

public sealed class TimerReducersTests : IDisposable
{
    private const string Key = "timer";

    private readonly TimerReducers _sut = new();
    private readonly TimerState _initialState = new() { Time = 0, IsRunning = false };

    private bool _disposed;

    [Fact]
    public void TimerReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void TimerReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void TimerReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(TimerState));
    }

    [Fact]
    public void TimerReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(4);
    }

    [Fact]
    public void StartTimer_ShouldSetIsRunningToTrue()
    {
        // Act
        var newState = _sut.Reduce(_initialState, new StartTimer());

        // Assert
        newState.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void StopTimer_ShouldSetIsRunningToFalse()
    {
        // Arrange
        var state = _initialState with { IsRunning = true };

        // Act
        var newState = _sut.Reduce(state, new StopTimer());

        // Assert
        newState.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ResetTimer_ShouldResetState()
    {
        // Arrange
        var state = new TimerState { Time = 5, IsRunning = true };

        // Act
        var newState = _sut.Reduce(state, new ResetTimer());

        // Assert
        newState.Time.Should().Be(0);
        newState.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Tick_ShouldIncrementTimeByOne()
    {
        // Arrange
        var state = new TimerState { Time = 5, IsRunning = true };

        // Act
        var newState = _sut.Reduce(state, new Tick());

        // Assert
        newState.Time.Should().Be(state.Time + 1);
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