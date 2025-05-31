using System.Collections.Concurrent;

namespace Ducky.Diagnostics;

/// <summary>
/// Provides diagnostic information about middleware execution.
/// </summary>
public class MiddlewareDiagnostics
{
    private readonly ConcurrentDictionary<Type, MiddlewareMetrics> _metrics = [];
    private readonly List<MiddlewareInfo> _registeredMiddlewares = [];

    /// <summary>
    /// Records that a middleware has been registered.
    /// </summary>
    public void RecordMiddlewareRegistration(Type middlewareType, int order)
    {
        _registeredMiddlewares.Add(new MiddlewareInfo
        {
            Type = middlewareType,
            Name = middlewareType.Name,
            Order = order,
            RegisteredAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Records the execution time of a middleware.
    /// </summary>
    public void RecordExecution(Type middlewareType, in TimeSpan duration, bool isBeforeReduce)
    {
        MiddlewareMetrics metrics = _metrics.GetOrAdd(middlewareType, _ => new MiddlewareMetrics());

        if (isBeforeReduce)
        {
            metrics.BeforeReduceExecutions++;
            metrics.TotalBeforeReduceTime += duration;
            metrics.LastBeforeReduceExecution = DateTimeOffset.UtcNow;
        }
        else
        {
            metrics.AfterReduceExecutions++;
            metrics.TotalAfterReduceTime += duration;
            metrics.LastAfterReduceExecution = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Records an error that occurred in a middleware.
    /// </summary>
    public void RecordError(Type middlewareType, Exception exception, bool isBeforeReduce)
    {
        MiddlewareMetrics metrics = _metrics.GetOrAdd(middlewareType, _ => new MiddlewareMetrics());
        metrics.Errors++;
        metrics.LastError = new ErrorInfo
        {
            Exception = exception,
            OccurredAt = DateTimeOffset.UtcNow,
            IsBeforeReduce = isBeforeReduce
        };
    }

    /// <summary>
    /// Gets a diagnostic report of all middleware activity.
    /// </summary>
    public MiddlewareDiagnosticReport GetReport()
    {
        List<MiddlewareReport> middlewareReports = _registeredMiddlewares
            .Select(info =>
            {
                _metrics.TryGetValue(info.Type, out MiddlewareMetrics? metrics);
                return new MiddlewareReport
                {
                    Info = info,
                    Metrics = metrics ?? new MiddlewareMetrics()
                };
            })
            .ToList();

        return new MiddlewareDiagnosticReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Middlewares = middlewareReports,
            TotalMiddlewares = _registeredMiddlewares.Count,
            TotalExecutions = middlewareReports.Sum(m => m.Metrics.TotalExecutions),
            TotalErrors = middlewareReports.Sum(m => m.Metrics.Errors)
        };
    }

    /// <summary>
    /// Resets all diagnostic data.
    /// </summary>
    public void Reset()
    {
        _metrics.Clear();
        _registeredMiddlewares.Clear();
    }
}

/// <summary>
/// Information about a registered middleware.
/// </summary>
public class MiddlewareInfo
{
    /// <summary>
    /// Gets the type of the middleware.
    /// </summary>
    public Type Type { get; init; } = null!;

    /// <summary>
    /// Gets the name of the middleware.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the registration order of the middleware.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the timestamp when the middleware was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; init; }
}

/// <summary>
/// Metrics for a specific middleware.
/// </summary>
public class MiddlewareMetrics
{
    /// <summary>
    /// Gets or sets the number of executions before state reduction.
    /// </summary>
    public long BeforeReduceExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of executions after state reduction.
    /// </summary>
    public long AfterReduceExecutions { get; set; }

    /// <summary>
    /// Gets the total number of executions (before + after).
    /// </summary>
    public long TotalExecutions => BeforeReduceExecutions + AfterReduceExecutions;

    /// <summary>
    /// Gets or sets the total time spent in before-reduce executions.
    /// </summary>
    public TimeSpan TotalBeforeReduceTime { get; set; }

    /// <summary>
    /// Gets or sets the total time spent in after-reduce executions.
    /// </summary>
    public TimeSpan TotalAfterReduceTime { get; set; }

    /// <summary>
    /// Gets the total execution time across all phases.
    /// </summary>
    public TimeSpan TotalExecutionTime => TotalBeforeReduceTime + TotalAfterReduceTime;

    /// <summary>
    /// Gets the average execution time for before-reduce phase.
    /// </summary>
    public TimeSpan AverageBeforeReduceTime => BeforeReduceExecutions > 0
        ? TimeSpan.FromTicks(TotalBeforeReduceTime.Ticks / BeforeReduceExecutions)
        : TimeSpan.Zero;

    /// <summary>
    /// Gets the average execution time for after-reduce phase.
    /// </summary>
    public TimeSpan AverageAfterReduceTime => AfterReduceExecutions > 0
        ? TimeSpan.FromTicks(TotalAfterReduceTime.Ticks / AfterReduceExecutions)
        : TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the timestamp of the last before-reduce execution.
    /// </summary>
    public DateTimeOffset? LastBeforeReduceExecution { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last after-reduce execution.
    /// </summary>
    public DateTimeOffset? LastAfterReduceExecution { get; set; }

    /// <summary>
    /// Gets or sets the total number of errors encountered.
    /// </summary>
    public long Errors { get; set; }

    /// <summary>
    /// Gets or sets information about the last error that occurred.
    /// </summary>
    public ErrorInfo? LastError { get; set; }
}

/// <summary>
/// Information about an error that occurred in a middleware.
/// </summary>
public class ErrorInfo
{
    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; init; } = null!;

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets a value indicating whether the error occurred during the before-reduce phase.
    /// </summary>
    public bool IsBeforeReduce { get; init; }
}

/// <summary>
/// A complete diagnostic report for all middlewares.
/// </summary>
public class MiddlewareDiagnosticReport
{
    /// <summary>
    /// Gets the timestamp when this report was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Gets the list of middleware reports.
    /// </summary>
    public List<MiddlewareReport> Middlewares { get; init; } = [];

    /// <summary>
    /// Gets the total number of registered middlewares.
    /// </summary>
    public int TotalMiddlewares { get; init; }

    /// <summary>
    /// Gets the total number of executions across all middlewares.
    /// </summary>
    public long TotalExecutions { get; init; }

    /// <summary>
    /// Gets the total number of errors across all middlewares.
    /// </summary>
    public long TotalErrors { get; init; }
}

/// <summary>
/// Report for a single middleware.
/// </summary>
public class MiddlewareReport
{
    /// <summary>
    /// Gets the middleware information.
    /// </summary>
    public MiddlewareInfo Info { get; init; } = null!;

    /// <summary>
    /// Gets the middleware execution metrics.
    /// </summary>
    public MiddlewareMetrics Metrics { get; init; } = null!;
}
