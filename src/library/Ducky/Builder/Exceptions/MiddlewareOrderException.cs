namespace Ducky.Builder;

/// <summary>
/// Exception thrown when middleware order violations are detected.
/// </summary>
public class MiddlewareOrderException : InvalidOperationException
{
    /// <summary>
    /// Gets the list of middleware order violations.
    /// </summary>
    public List<MiddlewareOrderViolation> Violations { get; }

    /// <summary>
    /// Initializes a new instance of the MiddlewareOrderException class.
    /// </summary>
    /// <param name="violations">The list of order violations.</param>
    public MiddlewareOrderException(List<MiddlewareOrderViolation> violations)
        : base(CreateMessage(violations))
    {
        Violations = violations;
    }

    /// <summary>
    /// Initializes a new instance of the MiddlewareOrderException class.
    /// </summary>
    public MiddlewareOrderException()
    {
        Violations = new List<MiddlewareOrderViolation>();
    }

    /// <summary>
    /// Initializes a new instance of the MiddlewareOrderException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MiddlewareOrderException(string message)
        : base(message)
    {
        Violations = new List<MiddlewareOrderViolation>();
    }

    /// <summary>
    /// Initializes a new instance of the MiddlewareOrderException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MiddlewareOrderException(string message, Exception innerException)
        : base(message, innerException)
    {
        Violations = new List<MiddlewareOrderViolation>();
    }

    private static string CreateMessage(List<MiddlewareOrderViolation> violations)
    {
        string suggestedOrder = GetSuggestedFixMessage(violations);

        return $$"""
                 Middleware order violations detected. The current order may cause issues.

                 Violations:
                 {{string.Join("\n", violations.Select(v => $"- {v}"))}}

                 {{suggestedOrder}}

                 To disable order validation (not recommended):
                 services.AddDuckyStore(builder => builder
                     .DisableOrderValidation()
                     // ... add middlewares
                 );
                 """;
    }

    private static string GetSuggestedFixMessage(List<MiddlewareOrderViolation> violations)
    {
        List<Type> allTypes = violations
            .SelectMany(v => new[] { v.MiddlewareType, v.RelatedType })
            .Distinct()
            .ToList();

        List<Type> suggestedOrder = MiddlewareOrderValidator.GetSuggestedOrder(allTypes);

        return $$"""
                 Suggested order:
                 services.AddDuckyStore(builder => builder
                 {{string.Join("\n", suggestedOrder.Select(t => $"    .Add{t.Name}()"))}}
                 );
                 """;
    }
}
