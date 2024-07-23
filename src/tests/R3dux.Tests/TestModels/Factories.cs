// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Moq;

namespace R3dux.Tests.TestModels;

internal static class Factories
{
    public static R3duxStore CreateTestCounterStore(IEffect[]? effects = null)
    {
        var dispatcher = new Dispatcher();
        var mockLogger = new Mock<ILogger<R3duxStore>>();
        var logger = mockLogger.Object;
        TestCounterReducers counterReducers = new();
        return StoreFactory.CreateStore(dispatcher, logger, [counterReducers], effects ?? []);
    }

    public static RootState CreateTestRootState()
    {
        const string testKey = "test-key";
        TestState initialState = new() { Value = 42 };

        var dictionary = ImmutableSortedDictionary<string, object>.Empty
            .Add(testKey, initialState);

        return new RootState(dictionary);
    }
}
