namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IStoreEventPublisher"/>.
/// </summary>
public sealed class StoreEventPublisher : IStoreEventPublisher, IDisposable
{
    /// <inheritdoc />
    public event EventHandler<StoreEventArgs>? EventPublished;

    /// <inheritdoc />
    public void Publish(StoreEventArgs storeEvent)
    {
        EventPublished?.Invoke(this, storeEvent);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Dispose()
    {
        EventPublished = null;
    }
}
