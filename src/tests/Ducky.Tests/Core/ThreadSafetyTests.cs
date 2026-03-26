// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Core;

#pragma warning disable SA1402, SA1649

public sealed record ThreadSafetyCounterState(int Count);

public sealed record IncrementAction;

public sealed record ThreadSafetyCounterReducers : SliceReducers<ThreadSafetyCounterState>
{
    public ThreadSafetyCounterReducers()
    {
        On<IncrementAction>((state, _) => state with { Count = state.Count + 1 });
    }

    public override ThreadSafetyCounterState GetInitialState()
        => new(0);
}

#pragma warning restore SA1402, SA1649

public class ThreadSafetyTests
{
    private static async Task<(IStore Store, IDispatcher Dispatcher)> CreateStore()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddScoped<ISlice, ThreadSafetyCounterReducers>();
        services.AddDucky(builder =>
        {
            builder.UseDefaultMiddlewares();
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();

        if (store is DuckyStore duckyStore && !duckyStore.IsInitialized)
        {
            await duckyStore.InitializeAsync();
        }

        return (store, dispatcher);
    }

    [Fact]
    public async Task ConcurrentDispatch_ShouldProcessAllActions()
    {
        // Arrange
        const int threadCount = 10;
        const int iterationsPerThread = 1000;
        const int expectedCount = threadCount * iterationsPerThread;

        (IStore store, IDispatcher dispatcher) = await CreateStore();
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act — dispatch from 10 threads x 1000 iterations each
        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(
                () =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        dispatcher.Dispatch(new IncrementAction());
                    }
                },
                ct);
        }

        await Task.WhenAll(tasks);

        // Assert — all increments should have been applied
        ThreadSafetyCounterState state = store.GetSlice<ThreadSafetyCounterState>();
        state.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public async Task ConcurrentDispatchAndRead_ShouldNotThrow()
    {
        // Arrange
        (IStore store, IDispatcher dispatcher) = await CreateStore();
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<Exception> exceptions = [];

        // Act — dispatch and read concurrently
        Task dispatchTask = Task.Run(
            () =>
            {
                for (int i = 0; i < 5000; i++)
                {
                    try
                    {
                        dispatcher.Dispatch(new IncrementAction());
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }
            },
            ct);

        Task readTask = Task.Run(
            () =>
            {
                for (int i = 0; i < 5000; i++)
                {
                    try
                    {
                        _ = store.GetSlice<ThreadSafetyCounterState>();
                        _ = store.GetSliceKeys();
                        _ = store.HasSlice<ThreadSafetyCounterState>();
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }
            },
            ct);

        await Task.WhenAll(dispatchTask, readTask);

        // Assert
        exceptions.ShouldBeEmpty();
    }
}
