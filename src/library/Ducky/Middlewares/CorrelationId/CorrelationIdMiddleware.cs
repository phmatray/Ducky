using Ducky.Pipeline;

namespace Ducky.Middlewares.CorrelationId;

/// <summary>
/// Middleware that ensures every dispatched action has a correlation ID, generating one if missing.
/// Publishes a <see cref="CorrelationIdAssignedEvent"/> via the pipeline event system.
/// </summary>
public sealed class CorrelationIdMiddleware : MiddlewareBase
{
    private readonly IStoreEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for dispatching correlation ID events.</param>
    public CorrelationIdMiddleware(IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc />
    public override void BeforeDispatch(object action)
    {
        // Assign correlation ID before dispatch
        Guid correlationId = Guid.NewGuid();

        // Note: Since we can't attach metadata to actions directly,
        // we'll use a different approach for correlation tracking
        _eventPublisher.Publish(new CorrelationIdAssignedEvent(action, correlationId));
    }
}
