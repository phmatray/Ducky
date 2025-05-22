using Ducky.Pipeline;

namespace Ducky.Middlewares.CorrelationId;

/// <summary>
/// Event published when a correlation ID is assigned to an action.
/// </summary>
public class CorrelationIdAssignedEvent : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdAssignedEvent"/> class.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="correlationId">The assigned correlation ID.</param>
    public CorrelationIdAssignedEvent(IActionContext context, in Guid correlationId)
    {
        Context = context;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// The action context.
    /// </summary>
    public IActionContext Context { get; init; }

    /// <summary>
    /// The assigned correlation ID.
    /// </summary>
    public Guid CorrelationId { get; init; }
}
