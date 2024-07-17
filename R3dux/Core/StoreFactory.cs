using Microsoft.Extensions.Logging;

namespace R3dux;

/// <summary>
/// Factory for creating instances of <see cref="Store"/>.
/// </summary>
public sealed class StoreFactory : IStoreFactory
{
    /// <inheritdoc />
    public Store CreateStore(
        IDispatcher dispatcher,
        ILogger<Store> logger,
        ISlice[] slices,
        IEffect[] effects)
    {
        var store = new Store(dispatcher, logger);

        store.AddSlices(slices);
        store.AddEffects(effects);

        return store;
    }
}