// using Ducky.Pipeline;
//
// namespace Ducky.Middlewares.CorrelationId;
//
// /// <summary>
// /// Middleware that ensures every dispatched action has a correlation ID, generating one if missing.
// /// Publishes a <see cref="CorrelationIdAssignedEvent"/> via the pipeline event system.
// /// </summary>
// public sealed class CorrelationIdMiddleware : StoreMiddleware
// {
//     /// <summary>
//     /// The metadata key used for the correlation ID.
//     /// </summary>
//     public const string CorrelationIdKey = "CorrelationId";
//
//     private readonly IPipelineEventPublisher _eventPublisher;
//
//     /// <summary>
//     /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
//     /// </summary>
//     /// <param name="eventPublisher">The event publisher for dispatching correlation ID events.</param>
//     public CorrelationIdMiddleware(IPipelineEventPublisher eventPublisher)
//     {
//         _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
//     }
//
//     /// <inheritdoc />
//     public override async Task BeforeDispatchAsync(
//         ActionContext context,
//         CancellationToken cancellationToken = default)
//     {
//         // Use an existing correlation ID if present, otherwise assign a new one
//         if (!context.Metadata.TryGetValue(CorrelationIdKey, out object? corrIdObj) || corrIdObj is not Guid)
//         {
//             Guid correlationId = Guid.NewGuid();
//             context.Metadata[CorrelationIdKey] = correlationId;
//
//             // Optionally: Publish event for observability
//             _eventPublisher.Publish(new CorrelationIdAssignedEvent(context, correlationId));
//         }
//         else
//         {
//             // Even if externally provided, you can still publish an event
//             if (corrIdObj is Guid existingId)
//             {
//                 _eventPublisher.Publish(new CorrelationIdAssignedEvent(context, existingId));
//             }
//         }
//
//         await Task.CompletedTask.ConfigureAwait(false);
//     }
// }
