using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace R3dux.Tests.TestModels;

internal static class Factories
{
    public static Store CreateTestCounterStore(IEffect[]? effects = null)
    {
        var storeFactory = new StoreFactory();
        var dispatcher = new Dispatcher();
        var logger = new Logger<Store>(new LoggerFactory());
        TestCounterReducers counterReducers = new();

        return storeFactory.CreateStore(dispatcher, logger, [counterReducers], effects ?? []);
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