using System.Collections.Immutable;

namespace R3dux.Tests.TestModels;

internal static class Factories
{
    public static Store CreateTestCounterStore(IEffect[]? effects = null)
    {
        IStoreFactory storeFactory = new StoreFactory();
        IDispatcher dispatcher = new Dispatcher();
        CounterSlice counterSlice = new();
        effects ??= [];
        
        return storeFactory.CreateStore(dispatcher, [counterSlice], effects);
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