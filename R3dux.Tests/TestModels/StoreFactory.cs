namespace R3dux.Tests.TestModels;

internal static class StoreFactory
{
    public static Store CreateTestCounterStore(
        int initialState = 0,
        IEffect[]? effects = null)
    {
        IDispatcher dispatcher = new Dispatcher();
        CounterSlice counterSlice = new();
        effects ??= [];
        
        return R3dux.StoreFactory.CreateStore(dispatcher, [counterSlice], effects);
    }
}