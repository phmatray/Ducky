// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.Core;

public class R3duxStoreTests
{
    private readonly R3duxStore _sut = Factories.CreateTestCounterStore();

    [Fact]
    public void Store_Should_Initialize_With_Default_State()
    {
        // Act
        var observable = _sut.RootStateObservable;
        var rootState = observable.FirstSync();

        // Assert
        rootState.Should().NotBeNull();
        rootState.Should().BeOfType<RootState>();
    }

    [Fact]
    public void Store_Should_Add_Slice_And_Propagate_State_Changes()
    {
        // Arrange
        var counterSlice = new TestCounterReducers();
        var sliceStateObs = _sut.RootStateObservable
            .Select(state => state.GetSliceState<int>("test-counter"));

        _sut.AddSlice(counterSlice);

        // Act
        counterSlice.OnDispatch(new TestIncrementAction());
        var updatedState = sliceStateObs.FirstSync();

        // Assert
        updatedState.Should().Be(11);
    }
}
