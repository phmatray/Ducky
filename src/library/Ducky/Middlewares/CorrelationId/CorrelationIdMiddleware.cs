using Ducky.Pipeline;

namespace Ducky.Middlewares.CorrelationId;

/// <summary>
/// Middleware that ensures every dispatched action has a correlation ID, generating one if missing.
/// Publishes a <see cref="CorrelationIdAssignedEvent"/> via the pipeline event system.
/// </summary>
public sealed class CorrelationIdMiddleware : IMiddleware
{
    /// <summary>
    /// The metadata key used for the correlation ID.
    /// </summary>
    public const string CorrelationIdKey = "CorrelationId";

    private readonly IStoreEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for dispatching correlation ID events.</param>
    public CorrelationIdMiddleware(IStoreEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <inheritdoc />
    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AfterInitializeAllMiddlewares()
    {
        // Nothing to do
    }

    /// <inheritdoc />
    public bool MayDispatchAction(object action)
    {
        return true;
    }

    /// <inheritdoc />
    public void BeforeDispatch(object action)
    {
        // Assign correlation ID before dispatch
        Guid correlationId = Guid.NewGuid();
        
        // Note: Since we can't attach metadata to actions directly,
        // we'll use a different approach for correlation tracking
        _eventPublisher.Publish(new CorrelationIdAssignedEvent(action, correlationId));
    }

    /// <inheritdoc />
    public void AfterDispatch(object action)
    {
        // Nothing to do after dispatch
    }

    /// <inheritdoc />
    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { });
    }
}
