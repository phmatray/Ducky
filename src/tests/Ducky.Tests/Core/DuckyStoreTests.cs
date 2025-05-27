// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Core;

public class DuckyStoreTests
{
    private readonly DuckyStore _sut = Factories.CreateTestCounterStore();

    [Fact]
    public void Store_Should_Initialize_With_Default_State()
    {
        // Act
        ReadOnlyReactiveProperty<IRootState> observable = _sut.RootStateObservable;
        IRootState rootState = observable.FirstSync();

        // Assert
        rootState.ShouldNotBeNull();
        rootState.ShouldBeOfType<RootState>();
    }

    [Fact]
    public void Store_Should_Add_Slice_And_Propagate_State_Changes()
    {
        // Arrange
        Dispatcher dispatcher = new();
        TestCounterReducers counterSlice = new();
        DuckyStore store = DuckyStoreFactory.CreateStore(dispatcher, [counterSlice]);

        Observable<int> sliceStateObs = store.RootStateObservable
            .Select(state => state.GetSliceState<int>("test-counter"));

        // Act
        dispatcher.Dispatch(new TestIncrementAction());

        // Wait for the action to be processed
        Thread.Sleep(50);

        int updatedState = sliceStateObs.FirstSync();

        // Assert
        updatedState.ShouldBe(11);
    }
}
