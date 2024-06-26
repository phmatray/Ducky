using Microsoft.Extensions.DependencyInjection;

namespace BlazorStore.Tests;

public class RxStoreExtensionsTests
{
    // private class TestState
    // {
    //     public int Value { get; set; }
    // }
    //
    // private class TestReducer : IActionReducer<TestState>
    // {
    //     public TestState Invoke(TestState state, IAction action)
    //     {
    //         return state; // Simplified for testing
    //     }
    // }
    //
    // [Fact]
    // public void AddRxStore_ShouldRegisterRxStore()
    // {
    //     var services = new ServiceCollection();
    //
    //     services.AddRxStore<TestState, TestReducer>();
    //
    //     var serviceProvider = services.BuildServiceProvider();
    //
    //     var store = serviceProvider.GetService<RxStore<TestState, TestReducer>>();
    //     store.Should().NotBeNull();
    //     store.Should().BeOfType<RxStore<TestState, TestReducer>>();
    // }
    //
    // [Fact]
    // public void AddRxStore_ShouldRegisterStateObservable()
    // {
    //     var services = new ServiceCollection();
    //
    //     services.AddRxStore<TestState, TestReducer>();
    //
    //     var serviceProvider = services.BuildServiceProvider();
    //
    //     var stateObservable = serviceProvider.GetService<IObservable<TestState>>();
    //     stateObservable.Should().NotBeNull();
    // }
    //
    // [Fact]
    // public void AddRxStore_ShouldRegisterActionsObservable()
    // {
    //     var services = new ServiceCollection();
    //
    //     services.AddRxStore<TestState, TestReducer>();
    //
    //     var serviceProvider = services.BuildServiceProvider();
    //
    //     var actionsObservable = serviceProvider.GetService<IObservable<IAction>>();
    //     actionsObservable.Should().NotBeNull();
    // }
    //
    // [Fact]
    // public void AddRxStore_ShouldBeSingleton()
    // {
    //     var services = new ServiceCollection();
    //
    //     services.AddRxStore<TestState, TestReducer>();
    //
    //     var serviceProvider = services.BuildServiceProvider();
    //
    //     var store1 = serviceProvider.GetService<RxStore<TestState, TestReducer>>();
    //     var store2 = serviceProvider.GetService<RxStore<TestState, TestReducer>>();
    //
    //     store1.Should().BeSameAs(store2);
    // }
}