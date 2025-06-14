// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.TestModels;

internal static class Factories
{
    public static IStore CreateTestCounterStore()
    {
        ServiceCollection services = [];

        // Add logging services for tests
        services.AddLogging();

        // Register the test counter slice
        services.AddScoped<ISlice, TestCounterReducers>();

        // Add Ducky store with minimal configuration
        services.AddDucky();

        ServiceProvider provider = services.BuildServiceProvider();
        IStore store = provider.GetRequiredService<IStore>();
        
        // Initialize store if it's a DuckyStore
        if (store is DuckyStore duckyStore && !duckyStore.IsInitialized)
        {
            duckyStore.InitializeAsync().GetAwaiter().GetResult();
        }
        
        return store;
    }

    public static RootState CreateTestRootState()
    {
        const string testKey = "test-key";
        TestState initialState = new() { Value = 42 };

        ImmutableSortedDictionary<string, object> dictionary = ImmutableSortedDictionary<string, object>.Empty
            .Add(testKey, initialState);

        return new RootState(dictionary);
    }
}
