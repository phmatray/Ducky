using Ducky.Pipeline;
using R3;

namespace Ducky.Middlewares.CorrelationId;

/// <summary>
/// Middleware that ensures every dispatched action has a correlation ID, generating one if missing.
/// Publishes a <see cref="CorrelationIdAssignedEvent"/> via the pipeline event system.
/// </summary>
public sealed class CorrelationIdMiddleware : IActionMiddleware
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
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        // Assign correlation ID before reducer processing
        return actions.Do(context =>
        {
            // Use an existing correlation ID if present, otherwise assign a new one
            if (!context.Metadata.TryGetValue(CorrelationIdKey, out object? corrIdObj) || corrIdObj is not Guid)
            {
                Guid correlationId = Guid.NewGuid();
                context.Metadata[CorrelationIdKey] = correlationId;

                // Publish event for observability
                _eventPublisher.Publish(new CorrelationIdAssignedEvent(context, correlationId));
            }
            else
            {
                // Even if externally provided, you can still publish an event
                if (corrIdObj is Guid existingId)
                {
                    _eventPublisher.Publish(new CorrelationIdAssignedEvent(context, existingId));
                }
            }
        });
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        return actions;
    }
}
