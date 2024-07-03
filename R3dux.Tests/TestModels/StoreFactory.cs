namespace R3dux.Tests.TestModels;

internal static class StoreFactory
{
    public static Store CreateTestCounterStore(
        int initialState = 0,
        IEffect[]? effects = null)
    {
        var counterSlice = new Slice<int>
        {
            Key = "counter",
            InitialState = initialState,
            Reducers = new CounterReducers(),
            Effects = effects ?? []
        };
        
        SliceCollection slices = [counterSlice];
        IDispatcher dispatcher = new Dispatcher();
        return new Store(slices, dispatcher);
    }
}