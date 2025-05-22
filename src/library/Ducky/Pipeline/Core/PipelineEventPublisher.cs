namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IPipelineEventPublisher"/>.
/// </summary>
public sealed class PipelineEventPublisher : IPipelineEventPublisher
{
    /// <inheritdoc />
    public event EventHandler<PipelineEventArgs>? EventPublished;

    /// <inheritdoc />
    public void Publish(PipelineEventArgs pipelineEvent)
    {
        EventPublished?.Invoke(this, pipelineEvent);
    }
}
