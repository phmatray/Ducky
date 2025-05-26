using R3;

namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IStoreEventPublisher"/>.
/// </summary>
public sealed class StoreEventPublisher : IStoreEventPublisher
{
    private readonly Subject<StoreEventArgs> _subject = new();

    /// <inheritdoc />
    public Observable<StoreEventArgs> Events
        => _subject.AsObservable();

    /// <inheritdoc />
    public void Publish(StoreEventArgs storeEvent)
    {
        _subject.OnNext(storeEvent);
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
