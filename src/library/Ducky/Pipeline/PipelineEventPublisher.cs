using R3;

namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IPipelineEventPublisher"/>.
/// </summary>
public sealed class PipelineEventPublisher : IPipelineEventPublisher
{
    private readonly Subject<PipelineEventArgs> _subject = new();

    /// <inheritdoc />
    public Observable<PipelineEventArgs> Events
        => _subject.AsObservable();

    /// <inheritdoc />
    public void Publish(PipelineEventArgs pipelineEvent)
    {
        _subject.OnNext(pipelineEvent);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Dispose()
    {
        _subject?.OnCompleted();
        _subject?.Dispose();
    }
}
