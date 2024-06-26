using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Xunit;

namespace BlazorStore.Tests;

public class RxStoreTests
{
    private class TestState
    {
        public int Value { get; set; }
    }

    private class TestAction : IAction
    {
        public int Amount { get; set; }
    }

    private class TestReducer : IActionReducer<TestState>
    {
        public TestState Invoke(TestState state, IAction action)
        {
            if (action is TestAction testAction)
            {
                return new TestState { Value = state.Value + testAction.Amount };
            }

            return state;
        }
    }

    // [Fact]
    // public async Task RxStore_InitialState_ShouldBeInitialized()
    // {
    //     var store = new RxStore<TestState, TestReducer>();
    //     var state = await store.State.FirstAsync();
    //
    //     state.Should().NotBeNull();
    //     state.Value.Should().Be(0); // Assuming initial state's Value is 0
    // }

    // [Fact]
    // public async Task RxStore_DispatchAction_ShouldUpdateState()
    // {
    //     var store = new RxStore<TestState, TestReducer>();
    //     var action = new TestAction { Amount = 5 };
    //
    //     store.Dispatch(action);
    //
    //     var state = await store.State.FirstAsync(s => s.Value == 5);
    //     state.Value.Should().Be(5);
    // }

    // [Fact]
    // public async Task RxStore_Select_ShouldReturnCorrectProjection()
    // {
    //     var store = new RxStore<TestState, TestReducer>();
    //     var action = new TestAction { Amount = 5 };
    //
    //     store.Dispatch(action);
    //
    //     var selectedValue = await store.Select(state => state.Value).FirstAsync();
    //     selectedValue.Should().Be(5);
    // }

    // [Fact]
    // public async Task RxStore_Actions_ShouldEmitDispatchedActions()
    // {
    //     var store = new RxStore<TestState, TestReducer>();
    //     var action = new TestAction { Amount = 5 };
    //
    //     IAction? receivedAction = null;
    //     store.Actions.Subscribe(a => receivedAction = a);
    //
    //     store.Dispatch(action);
    //
    //     receivedAction.Should().BeOfType<TestAction>();
    //     (receivedAction as TestAction).Amount.Should().Be(5);
    // }

    // [Fact]
    // public void RxStore_Reduce_ShouldLogStateChange()
    // {
    //     var store = new RxStore<TestState, TestReducer>();
    //     var action = new TestAction { Amount = 5 };
    //
    //     // Assuming StateLogger is a static class that logs state changes
    //     var logEntries = new List<string>();
    //     StateLogger.LogStateChange = (a, oldState, newState, elapsed) =>
    //     {
    //         logEntries.Add($"Action: {a}, OldState: {oldState.Value}, NewState: {newState.Value}, Time: {elapsed}");
    //     };
    //
    //     store.Dispatch(action);
    //
    //     logEntries.Should().HaveCount(1);
    //     logEntries[0].Should().Contain("Action:");
    //     logEntries[0].Should().Contain("OldState: 0");
    //     logEntries[0].Should().Contain("NewState: 5");
    // }
}
