using Microsoft.Extensions.Logging;

namespace R3dux;

/// <summary>
/// Factory interface for creating instances of <see cref="Store"/>.
/// </summary>
public interface IStoreFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="Store"/>.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to be used by the store.</param>
    /// <param name="logger">The logger to be used by the store.</param>
    /// <param name="slices">The collection of slices to be added to the store.</param>
    /// <param name="effects">The collection of effects to be added to the store.</param>
    /// <returns>A new instance of <see cref="Store"/>.</returns>
    Store CreateStore(
        IDispatcher dispatcher,
        ILogger<Store> logger,
        ISlice[] slices,
        IEffect[] effects);
}