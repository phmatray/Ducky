namespace R3dux;

/// <summary>
/// Factory for creating instances of <see cref="Store"/>.
/// </summary>
public class StoreFactory : IStoreFactory
{
    /// <inheritdoc />
    public Store CreateStore(
        IDispatcher dispatcher,
        IEnumerable<ISlice> slices,
        IEnumerable<IEffect> effects)
    {
        var store = new Store(dispatcher);

        store.AddSlices(slices);
        store.AddEffects(effects);

        return store;
    }
}