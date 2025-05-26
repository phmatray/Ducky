using Ducky.Pipeline;
using R3;

namespace Ducky;

/// <summary>
/// Publishes store events and allows subscribers to listen for them.
/// </summary>
public interface IStoreEventPublisher
{
    /// <summary>
    /// Get an observable for pipeline events.
    /// </summary>
    Observable<StoreEventArgs> Events { get; }

    /// <summary>
    /// Publishes a new pipeline event to all subscribers.
    /// </summary>
    /// <param name="storeEvent">The event instance to publish.</param>
    void Publish(StoreEventArgs storeEvent);
}
