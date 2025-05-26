// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Counter;

public sealed class CounterEffectsTests
{
    private readonly ResetCounterAfter3Sec _sut;
    private readonly FakeTimeProvider _timeProvider = new();

    public CounterEffectsTests()
    {
        ObservableSystem.DefaultTimeProvider = _timeProvider;

        _sut = new ResetCounterAfter3Sec();
    }

    [Fact]
    public void IncrementEffect_ShouldEmitResetAction_WhenValueGreaterThan15()
    {
        // Arrange
        RootState rootState = GetRootState(16);

        // Act
        _ = _sut.HandleAsync(new Increment(), rootState);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Assert
        _sut.LastAction.ShouldBeOfType<Reset>();
    }

    [Fact]
    public void IncrementEffect_ShouldNotEmitResetAction_WhenValueIs15OrLess()
    {
        // Arrange
        RootState rootState = GetRootState(15);

        // Act
        _ = _sut.HandleAsync(new Increment(), rootState);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Assert
        _sut.LastAction.ShouldBeNull();
    }

    private static RootState GetRootState(int counterValue)
    {
        ImmutableSortedDictionary<string, object> dictionary = ImmutableSortedDictionary
            .Create<string, object>()
            .Add("counter", new CounterState(counterValue));

        return new RootState(dictionary);
    }
}
