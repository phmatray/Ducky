// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.TestModels;

internal static class Factories
{
    public static DuckyStore CreateTestCounterStore(
        IEffect[]? effects = null,
        IReactiveEffect[]? reactiveEffects = null)
    {
        Dispatcher dispatcher = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(Configure);
        LoggerProvider.Configure(loggerFactory);
        TestCounterReducers counterReducers = new();
        return StoreFactory.CreateStore(dispatcher, [counterReducers], effects ?? [], reactiveEffects ?? []);
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
