// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Core;

public class DuckyStoreApiTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IStore _store;
    private readonly IDispatcher _dispatcher;

    public DuckyStoreApiTests()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder.ScanAssemblies(typeof(TestCounterReducers).Assembly));
        
        _provider = services.BuildServiceProvider();
        _store = _provider.GetRequiredService<IStore>();
        _dispatcher = _provider.GetRequiredService<IDispatcher>();
        
        // Initialize store if it's a DuckyStore
        if (_store is not DuckyStore duckyStore || duckyStore.IsInitialized)
        {
            return;
        }
            
        duckyStore.InitializeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void GetSlice_Should_Return_Typed_Slice_State()
    {
        // Act
        int counterState = _store.GetSlice<int>();

        // Assert
        counterState.ShouldBe(10); // Initial state from TestCounterReducers
    }

    [Fact]
    public void GetSlice_Should_Throw_When_Slice_Not_Found()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _store.GetSlice<string>());
    }

    [Fact]
    public void TryGetSlice_Should_Return_True_When_Slice_Exists()
    {
        // Act
        bool result = _store.TryGetSlice(out int counterState);

        // Assert
        result.ShouldBeTrue();
        counterState.ShouldBe(10);
    }

    [Fact]
    public void TryGetSlice_Should_Return_False_When_Slice_Not_Found()
    {
        // Act
        bool result = _store.TryGetSlice(out string? state);

        // Assert
        result.ShouldBeFalse();
        state.ShouldBeNull();
    }

    [Fact]
    public void HasSlice_Should_Return_True_When_Slice_Exists()
    {
        // Act
        bool hasSlice = _store.HasSlice<int>();

        // Assert
        hasSlice.ShouldBeTrue();
    }

    [Fact]
    public void HasSlice_Should_Return_False_When_Slice_Not_Found()
    {
        // Act
        bool hasSlice = _store.HasSlice<string>();

        // Assert
        hasSlice.ShouldBeFalse();
    }

    [Fact]
    public void GetSliceNames_Should_Return_All_Registered_Slice_Names()
    {
        // Act
        IReadOnlyList<string> sliceNames = _store.GetSliceNames();

        // Assert
        sliceNames.ShouldNotBeEmpty();
        sliceNames.ShouldContain("ducky-tests-test-models-test-counter");
    }

    [Fact]
    public void WhenSliceChanges_Should_Subscribe_To_Slice_State_Changes()
    {
        // Arrange
        List<int> states = [];
        using IDisposable subscription = _store.WhenSliceChanges<int>(states.Add);

        // Act
        _dispatcher.Dispatch(new TestIncrementAction());
        _dispatcher.Dispatch(new TestIncrementAction());

        // Give time for state updates
        Thread.Sleep(100);

        // Assert
        states.Count.ShouldBeGreaterThan(0);
        states.ShouldContain(11); // After first increment
        states.ShouldContain(12); // After second increment
    }

    [Fact]
    public void WhenSliceChanges_With_Selector_Should_Transform_State()
    {
        // Arrange
        List<string> messages = [];
        using IDisposable subscription = _store.WhenSliceChanges<int, string>(
            counter => $"Count: {counter}",
            messages.Add
        );

        // Act
        _dispatcher.Dispatch(new TestIncrementAction());

        // Give time for state update
        Thread.Sleep(100);

        // Assert
        messages.ShouldContain("Count: 11");
    }

    [Fact]
    public void WhenSliceChanges_Should_Only_Notify_On_Actual_Changes()
    {
        // Arrange
        List<int> states = [];
        using IDisposable subscription = _store.WhenSliceChanges<int>(states.Add);

        // Act
        _dispatcher.Dispatch(new TestSetValueAction(20));
        _dispatcher.Dispatch(new TestSetValueAction(20)); // Same value - should not trigger
        _dispatcher.Dispatch(new TestSetValueAction(21));

        // Give time for state updates
        Thread.Sleep(100);

        // Assert
        states.Count.ShouldBe(2); // Only 20 and 21, not duplicate 20
        states.ShouldContain(20);
        states.ShouldContain(21);
    }

    [Fact]
    public void IsInitialized_Should_Be_True_After_Construction()
    {
        // Assert
        _store.IsInitialized.ShouldBeTrue();
    }

    [Fact]
    public void StartTime_Should_Be_Set_On_Construction()
    {
        // Arrange
        DateTime beforeCreation = DateTime.UtcNow;

        // Act
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder.ScanAssemblies(typeof(TestCounterReducers).Assembly));
        using ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();

        // Assert
        store.StartTime.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        store.StartTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public void SliceCount_Should_Return_Number_Of_Registered_Slices()
    {
        // Assert
        _store.SliceCount.ShouldBeGreaterThan(0);
        _store.SliceCount.ShouldBe(_store.GetSliceNames().Count);
    }

    [Fact]
    public void GetSliceByKey_Should_Return_Slice_State_By_Key()
    {
        // Act
        int counterState = _store.GetSliceByKey<int>("ducky-tests-test-models-test-counter");

        // Assert
        counterState.ShouldBe(10);
    }

    [Fact]
    public void GetSliceByKey_Should_Throw_When_Key_Not_Found()
    {
        // Act & Assert
        Should.Throw<KeyNotFoundException>(() => _store.GetSliceByKey<int>("non-existent-key"));
    }

    [Fact]
    public void WhenSliceChanges_Should_Unsubscribe_When_Disposed()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder => builder.ScanAssemblies(typeof(TestCounterReducers).Assembly));
        using ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        
        List<int> states = [];
        IDisposable subscription = store.WhenSliceChanges<int>(states.Add);
        
        // Verify subscription is working
        dispatcher.Dispatch(new TestIncrementAction());
        Thread.Sleep(50);
        int beforeDisposeCount = states.Count;

        // Act
        subscription.Dispose();
        dispatcher.Dispatch(new TestIncrementAction());
        Thread.Sleep(50);

        // Assert
        states.Count.ShouldBe(beforeDisposeCount); // No new states after dispose
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}