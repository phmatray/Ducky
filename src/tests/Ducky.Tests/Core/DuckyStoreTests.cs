// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Core;

public class DuckyStoreTests
{
    private readonly IStore _sut = Factories.CreateTestCounterStore();

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
        ServiceCollection services = [];
        services.AddLogging();
        // TestCounterReducers will be automatically registered by assembly scanning
        services.AddDuckyStore(builder => { });

        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();

        // IMPORTANT: The store is fully initialized when GetRequiredService<IStore>() returns
        // because DuckyStore constructor:
        // 1. Registers all slices synchronously
        // 2. Calls pipeline.InitializeAsync(...).Wait() - blocking until complete
        // 3. Dispatches StoreInitialized action
        // 4. Publishes StoreInitializedEventArgs event
        // Therefore, no additional waiting is needed.

        // Get initial state to verify slice is loaded correctly
        int initialState = store.RootStateObservable
            .Select(state => state.GetSliceState<int>("ducky-tests-test-models-test-counter"))
            .FirstSync();
        
        initialState.ShouldBe(10); // Verify initial state
        
        // Act
        dispatcher.Dispatch(new TestIncrementAction());

        // Wait for the state change by observing until we get the expected value
        // This approach is more reliable than Thread.Sleep as it waits for the actual change
        int updatedState = store.RootStateObservable
            .Select(state => state.GetSliceState<int>("ducky-tests-test-models-test-counter"))
            .Where(value => value != initialState) // Wait for state to change
            .FirstSync();

        // Assert
        updatedState.ShouldBe(11);
    }
}
