namespace R3dux;

/// <summary>
/// Factory for creating instances of <see cref="Store"/>.
/// </summary>
public sealed class StoreFactory : IStoreFactory
{
    /// <inheritdoc />
    public Store CreateStore(
        IDispatcher dispatcher,
        ISlice[] slices,
        IEffect[] effects)
    {
        var store = new Store(dispatcher);

        store.AddSlices(slices);
        store.AddEffects(effects);

        return store;
    }
}