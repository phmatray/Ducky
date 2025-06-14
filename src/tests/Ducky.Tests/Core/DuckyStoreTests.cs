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
        IStateProvider stateProvider = _sut;

        // Assert
        stateProvider.ShouldNotBeNull();
        stateProvider.ShouldBeOfType<DuckyStore>();
    }

    [Fact]
    public void Store_Should_Add_Slice_And_Propagate_State_Changes()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        // TestCounterReducers will be automatically registered by assembly scanning
        services.AddDucky(builder => builder.ScanAssemblies(typeof(TestCounterReducers).Assembly));

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
        int initialState = store.GetSliceByKey<int>("ducky-tests-test-models-test-counter");

        initialState.ShouldBe(10); // Verify initial state

        // Subscribe to state changes before dispatching
        var stateChanged = false;
        int updatedState = initialState;
        store.StateChanged += (_, args) =>
        {
            updatedState = store.GetSliceByKey<int>("ducky-tests-test-models-test-counter");
            if (updatedState == initialState)
            {
                return;
            }

            stateChanged = true;
        };

        // Act
        dispatcher.Dispatch(new TestIncrementAction());

        // Give some time for the state to update
        for (int i = 0; i < 10 && !stateChanged; i++)
        {
            Thread.Sleep(50);
        }

        // Assert
        stateChanged.ShouldBeTrue();
        updatedState.ShouldBe(11);
    }
}
