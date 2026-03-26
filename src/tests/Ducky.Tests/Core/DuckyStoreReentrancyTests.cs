// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Core;

#pragma warning disable SA1402, SA1649

// Test actions for re-entrancy scenarios
public sealed record TriggerReentrantAction;

public sealed record ReentrantFollowUpAction;

public sealed record ReentrantChainAction(int Depth);

public sealed record FloodAction(int Count);

public sealed record FloodedAction(int Index);

/// <summary>
/// A middleware that synchronously dispatches during AfterReduce
/// to trigger re-entrancy.
/// </summary>
public sealed class ReentrantMiddleware : IMiddleware
{
    private IDispatcher? _dispatcher;

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _dispatcher = dispatcher;
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
    }

    public bool MayDispatchAction(object action) => true;

    public void BeforeReduce(object action)
    {
    }

    public void AfterReduce(object action)
    {
        if (action is TriggerReentrantAction)
        {
            _dispatcher!.Dispatch(new ReentrantFollowUpAction());
        }
        else if (action is ReentrantChainAction { Depth: > 0 } chain)
        {
            _dispatcher!.Dispatch(
                new ReentrantChainAction(chain.Depth - 1));
        }
        else if (action is FloodAction flood)
        {
            // Dispatch many actions at once to fill the queue
            for (int i = 0; i < flood.Count; i++)
            {
                _dispatcher!.Dispatch(new FloodedAction(i));
            }
        }
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { });
    }
}

// Reducers that track which actions were processed and in what order
public sealed record ReentrancyTestState(List<string> ProcessedActions);

public sealed record ReentrancyTestReducers
    : SliceReducers<ReentrancyTestState>
{
    public ReentrancyTestReducers()
    {
        On<TriggerReentrantAction>((state, _) =>
        {
            state.ProcessedActions.Add(
                nameof(TriggerReentrantAction));
            return state;
        });
        On<ReentrantFollowUpAction>((state, _) =>
        {
            state.ProcessedActions.Add(
                nameof(ReentrantFollowUpAction));
            return state;
        });
        On<ReentrantChainAction>((state, action) =>
        {
            state.ProcessedActions.Add(
                $"{nameof(ReentrantChainAction)}({action.Depth})");
            return state;
        });
    }

    public override ReentrancyTestState GetInitialState()
    {
        return new ReentrancyTestState([]);
    }
}

#pragma warning restore SA1402, SA1649

public class DuckyStoreReentrancyTests
{
    private static async Task<(
        IStore Store,
        IDispatcher Dispatcher,
        IStoreEventPublisher EventPublisher)>
        CreateStoreWithReentrantMiddleware()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddScoped<ISlice, ReentrancyTestReducers>();
        services.AddScoped<ReentrantMiddleware>();
        services.AddDucky(builder =>
        {
            builder.AddMiddleware<ReentrantMiddleware>();
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher =
            provider.GetRequiredService<IDispatcher>();
        IStoreEventPublisher eventPublisher =
            provider.GetRequiredService<IStoreEventPublisher>();

        if (store is DuckyStore duckyStore
            && !duckyStore.IsInitialized)
        {
            await duckyStore.InitializeAsync();
        }

        return (store, dispatcher, eventPublisher);
    }

    [Fact]
    public async Task ReentrantDispatch_IsQueued_AndProcessed()
    {
        // Arrange
        (IStore store, IDispatcher dispatcher, _) =
            await CreateStoreWithReentrantMiddleware();

        // Act — triggers re-entrant dispatch in AfterReduce
        dispatcher.Dispatch(new TriggerReentrantAction());

        // Assert — both actions should have been processed
        ReentrancyTestState state =
            store.GetSlice<ReentrancyTestState>();
        state.ProcessedActions
            .ShouldContain(nameof(TriggerReentrantAction));
        state.ProcessedActions
            .ShouldContain(nameof(ReentrantFollowUpAction));
    }

    [Fact]
    public async Task ReentrantDispatch_PublishesReentrantEvent()
    {
        // Arrange
        (IStore store,
            IDispatcher dispatcher,
            IStoreEventPublisher eventPublisher) =
            await CreateStoreWithReentrantMiddleware();

        ActionReentrantEventArgs? capturedEvent = null;
        eventPublisher.EventPublished += (_, args) =>
        {
            if (args is not ActionReentrantEventArgs reentrant)
            {
                return;
            }

            capturedEvent = reentrant;
        };

        // Act
        dispatcher.Dispatch(new TriggerReentrantAction());

        // Assert
        capturedEvent.ShouldNotBeNull();
        capturedEvent.Action
            .ShouldBeOfType<ReentrantFollowUpAction>();
        capturedEvent.CurrentAction
            .ShouldBeOfType<TriggerReentrantAction>();
        capturedEvent.QueueDepth.ShouldBe(1);
    }

    [Fact]
    public async Task ReentrantDispatch_ProcessesInFifoOrder()
    {
        // Arrange
        (IStore store, IDispatcher dispatcher, _) =
            await CreateStoreWithReentrantMiddleware();

        // Act — chain: 3 → 2 → 1 → 0
        dispatcher.Dispatch(new ReentrantChainAction(3));

        // Assert — all processed in FIFO order
        ReentrancyTestState state =
            store.GetSlice<ReentrancyTestState>();

        state.ProcessedActions
            .ShouldContain(
                $"{nameof(ReentrantChainAction)}(3)");
        state.ProcessedActions
            .ShouldContain(
                $"{nameof(ReentrantChainAction)}(2)");
        state.ProcessedActions
            .ShouldContain(
                $"{nameof(ReentrantChainAction)}(1)");
        state.ProcessedActions
            .ShouldContain(
                $"{nameof(ReentrantChainAction)}(0)");

        // Verify FIFO ordering
        int idx3 = state.ProcessedActions.IndexOf(
            $"{nameof(ReentrantChainAction)}(3)");
        int idx2 = state.ProcessedActions.IndexOf(
            $"{nameof(ReentrantChainAction)}(2)");
        int idx1 = state.ProcessedActions.IndexOf(
            $"{nameof(ReentrantChainAction)}(1)");
        int idx0 = state.ProcessedActions.IndexOf(
            $"{nameof(ReentrantChainAction)}(0)");

        idx3.ShouldBeLessThan(idx2);
        idx2.ShouldBeLessThan(idx1);
        idx1.ShouldBeLessThan(idx0);
    }

    [Fact]
    public async Task ReentrantDispatch_ThrowsWhenMaxDepthExceeded()
    {
        // Arrange
        (IStore store,
            IDispatcher dispatcher,
            IStoreEventPublisher eventPublisher) =
            await CreateStoreWithReentrantMiddleware();

        // Act & Assert — flood with 12 actions during single AfterReduce
        // exceeds MaxReentrantDepth (10), should throw InvalidOperationException
        Should.Throw<InvalidOperationException>(
            () => dispatcher.Dispatch(new FloodAction(12)));
    }
}
