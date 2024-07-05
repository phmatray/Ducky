namespace R3dux.Tests.TestModels;

internal static class TestStoreFactory
{
    public static Store CreateTestCounterStore(
        int initialState = 0,
        IEffect[]? effects = null)
    {
        IStoreFactory storeFactory = new StoreFactory();
        IDispatcher dispatcher = new Dispatcher();
        CounterSlice counterSlice = new();
        effects ??= [];
        
        return storeFactory.CreateStore(dispatcher, [counterSlice], effects);
    }
}