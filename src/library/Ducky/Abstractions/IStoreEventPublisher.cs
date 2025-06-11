using Ducky.Pipeline;

namespace Ducky;

/// <summary>
/// Publishes store events and allows subscribers to listen for them.
/// </summary>
public interface IStoreEventPublisher
{
    /// <summary>
    /// Occurs when a store event is published.
    /// </summary>
    event EventHandler<StoreEventArgs>? EventPublished;

    /// <summary>
    /// Publishes a new pipeline event to all subscribers.
    /// </summary>
    /// <param name="storeEvent">The event instance to publish.</param>
    void Publish(StoreEventArgs storeEvent);
}
