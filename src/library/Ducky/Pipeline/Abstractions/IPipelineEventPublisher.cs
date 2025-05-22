namespace Ducky.Pipeline;

/// <summary>
/// Publishes pipeline events and allows subscribers to listen for them.
/// </summary>
public interface IPipelineEventPublisher
{
    /// <summary>
    /// Publishes a new pipeline event to all subscribers.
    /// </summary>
    /// <param name="pipelineEvent">The event instance to publish.</param>
    void Publish(PipelineEventArgs pipelineEvent);

    /// <summary>
    /// Occurs when a new pipeline event is published.
    /// </summary>
    event EventHandler<PipelineEventArgs>? EventPublished;
}
