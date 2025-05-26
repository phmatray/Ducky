using Ducky.Pipeline;
using R3;

namespace Ducky;

/// <summary>
/// Publishes pipeline events and allows subscribers to listen for them.
/// </summary>
public interface IPipelineEventPublisher
{
    /// <summary>
    /// Get an observable for pipeline events.
    /// </summary>
    Observable<PipelineEventArgs> Events { get; }

    /// <summary>
    /// Publishes a new pipeline event to all subscribers.
    /// </summary>
    /// <param name="pipelineEvent">The event instance to publish.</param>
    void Publish(PipelineEventArgs pipelineEvent);
}
