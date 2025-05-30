using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Middlewares.ReactiveEffect;

namespace Ducky.Builder;

/// <summary>
/// Validates the order of middlewares in the pipeline to ensure correct behavior.
/// </summary>
internal static class MiddlewareOrderValidator
{
    private static readonly Dictionary<Type, MiddlewareOrderRule?> OrderRules = new()
    {
        [typeof(CorrelationIdMiddleware)] = new MiddlewareOrderRule
        {
            Priority = 100,
            ShouldComeBefore = new HashSet<Type> {
                typeof(ExceptionHandlingMiddleware),
                typeof(AsyncEffectMiddleware),
                typeof(ReactiveEffectMiddleware) },
            Reason = "CorrelationId should be set early to track actions through the entire pipeline"
        },
        [typeof(ExceptionHandlingMiddleware)] = new MiddlewareOrderRule
        {
            Priority = 200,
            ShouldComeBefore = new HashSet<Type> { typeof(AsyncEffectMiddleware), typeof(ReactiveEffectMiddleware) },
            ShouldComeAfter = new HashSet<Type> { typeof(CorrelationIdMiddleware) },
            Reason = "ExceptionHandling should wrap effects to catch and handle their errors"
        },
        [typeof(AsyncEffectMiddleware)] = new MiddlewareOrderRule
        {
            Priority = 300,
            ShouldComeAfter = new HashSet<Type> { typeof(CorrelationIdMiddleware), typeof(ExceptionHandlingMiddleware) },
            Reason = "AsyncEffect should run after correlation and error handling are set up"
        },
        [typeof(ReactiveEffectMiddleware)] = new MiddlewareOrderRule
        {
            Priority = 400,
            ShouldComeAfter = new HashSet<Type> { typeof(CorrelationIdMiddleware), typeof(ExceptionHandlingMiddleware) },
            Reason = "ReactiveEffect should run after correlation and error handling are set up"
        }
    };

    /// <summary>
    /// Validates the middleware order and returns any violations found.
    /// </summary>
    /// <param name="middlewareTypes">The ordered list of middleware types.</param>
    /// <returns>A list of order violations, if any.</returns>
    public static List<MiddlewareOrderViolation> Validate(IReadOnlyList<Type> middlewareTypes)
    {
        List<MiddlewareOrderViolation> violations = [];
        Dictionary<Type, int> typeToIndex = middlewareTypes
            .Select((type, index) => (type, index))
            .ToDictionary(x => x.type, x => x.index);

        foreach ((Type middlewareType, int index) in typeToIndex)
        {
            if (!OrderRules.TryGetValue(middlewareType, out MiddlewareOrderRule? rule) || rule is null)
            {
                continue;
            }

            // Check "should come before" rules
            foreach (Type shouldComeBefore in rule.ShouldComeBefore)
            {
                if (typeToIndex.TryGetValue(shouldComeBefore, out int otherIndex) && index > otherIndex)
                {
                    violations.Add(new MiddlewareOrderViolation
                    {
                        MiddlewareType = middlewareType,
                        ViolationType = OrderViolationType.ShouldComeBefore,
                        RelatedType = shouldComeBefore,
                        CurrentIndex = index,
                        ExpectedRelativePosition = "before",
                        Reason = rule.Reason
                    });
                }
            }

            // Check "should come after" rules
            foreach (Type shouldComeAfter in rule.ShouldComeAfter)
            {
                if (typeToIndex.TryGetValue(shouldComeAfter, out int otherIndex) && index < otherIndex)
                {
                    violations.Add(new MiddlewareOrderViolation
                    {
                        MiddlewareType = middlewareType,
                        ViolationType = OrderViolationType.ShouldComeAfter,
                        RelatedType = shouldComeAfter,
                        CurrentIndex = index,
                        ExpectedRelativePosition = "after",
                        Reason = rule.Reason
                    });
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Gets the suggested order for a list of middleware types based on their priorities.
    /// </summary>
    /// <param name="middlewareTypes">The middleware types to order.</param>
    /// <returns>The suggested order of middleware types.</returns>
    public static List<Type> GetSuggestedOrder(IEnumerable<Type> middlewareTypes)
    {
        return middlewareTypes
            .OrderBy(type => OrderRules.TryGetValue(type, out MiddlewareOrderRule? rule) ? rule?.Priority ?? int.MaxValue : int.MaxValue)
            .ToList();
    }
}

/// <summary>
/// Represents a rule for middleware ordering.
/// </summary>
internal class MiddlewareOrderRule
{
    public int Priority { get; init; }
    public HashSet<Type> ShouldComeBefore { get; init; } = [];
    public HashSet<Type> ShouldComeAfter { get; init; } = [];
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Represents a middleware order violation.
/// </summary>
public class MiddlewareOrderViolation
{
    /// <summary>
    /// Gets or sets the type of middleware that has the violation.
    /// </summary>
    public Type MiddlewareType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the type of violation.
    /// </summary>
    public OrderViolationType ViolationType { get; init; }

    /// <summary>
    /// Gets or sets the type of middleware this is related to.
    /// </summary>
    public Type RelatedType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the current index of the middleware.
    /// </summary>
    public int CurrentIndex { get; init; }

    /// <summary>
    /// Gets or sets the expected relative position.
    /// </summary>
    public string ExpectedRelativePosition { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason for the violation.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Returns a string representation of the violation.
    /// </summary>
    /// <returns>A descriptive string of the violation.</returns>
    public override string ToString()
    {
        return $"{MiddlewareType.Name} should come {ExpectedRelativePosition} {RelatedType.Name}. {Reason}";
    }
}

/// <summary>
/// Types of order violations.
/// </summary>
public enum OrderViolationType
{
    /// <summary>
    /// Indicates that a middleware should come before another middleware but doesn't.
    /// </summary>
    ShouldComeBefore,

    /// <summary>
    /// Indicates that a middleware should come after another middleware but doesn't.
    /// </summary>
    ShouldComeAfter
}
