// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares;
using Ducky.Pipeline;

namespace Ducky.Tests.TestModels;

internal static class Factories
{
    public static DuckyStore CreateTestCounterStore(
        IAsyncEffect[]? asyncEffects = null,
        IReactiveEffect[]? reactiveEffects = null,
        IStoreMiddleware[]? middlewares = null)
    {
        Dispatcher dispatcher = new();
        PipelineEventPublisher pipelineEventPublisher = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(Configure);
        LoggerProvider.Configure(loggerFactory);
        TestCounterReducers counterReducers = new();

        return DuckyStoreFactory.CreateStore(
            dispatcher,
            [counterReducers],
            asyncEffects ?? [],
            reactiveEffects ?? [],
            middlewares ?? []);
    }

    public static RootState CreateTestRootState()
    {
        const string testKey = "test-key";
        TestState initialState = new() { Value = 42 };

        ImmutableSortedDictionary<string, object> dictionary = ImmutableSortedDictionary<string, object>.Empty
            .Add(testKey, initialState);

        return new RootState(dictionary);
    }

    private static void Configure(ILoggingBuilder obj)
    {
    }
}
