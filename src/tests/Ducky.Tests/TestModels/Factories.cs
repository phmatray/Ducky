// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.AsyncEffect;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.TestModels;

internal static class Factories
{
    public static DuckyStore CreateTestCounterStore()
    {
        Dispatcher dispatcher = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(Configure);
        LoggerProvider.Configure(loggerFactory);
        TestCounterReducers counterReducers = new();

        // Use a basic ServiceProvider for tests
        ServiceCollection services = [];
        services.AddAsyncEffectMiddleware();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        return DuckyStoreFactory.CreateStore(
            dispatcher,
            [counterReducers],
            pipeline =>
            {
                pipeline.Use(serviceProvider.GetRequiredService<AsyncEffectMiddleware>());
            });
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
