using System.Diagnostics;
using Ducky.Builder;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.Diagnostics;

/// <summary>
/// A middleware that wraps other middlewares to collect diagnostic information.
/// </summary>
public class DiagnosticMiddleware : IActionMiddleware
{
    private readonly IActionMiddleware _innerMiddleware;
    private readonly MiddlewareDiagnostics _diagnostics;
    private readonly Type _middlewareType;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticMiddleware"/> class.
    /// </summary>
    /// <param name="innerMiddleware">The middleware to wrap and collect diagnostics for.</param>
    /// <param name="diagnostics">The diagnostics service to record metrics.</param>
    public DiagnosticMiddleware(
        IActionMiddleware innerMiddleware,
        MiddlewareDiagnostics diagnostics)
    {
        _innerMiddleware = innerMiddleware;
        _diagnostics = diagnostics;
        _middlewareType = innerMiddleware.GetType();
    }

    /// <summary>
    /// Invokes the wrapped middleware before state reduction and records execution metrics.
    /// </summary>
    /// <param name="actions">The stream of action contexts to process.</param>
    /// <returns>The processed action contexts.</returns>
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        return actions.Select(context =>
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ActionContext result = _innerMiddleware.InvokeBeforeReduce(Observable.Return(context))
                    .FirstOrDefaultAsync()
                    .GetAwaiter()
                    .GetResult();

                _diagnostics.RecordExecution(_middlewareType, stopwatch.Elapsed, true);
                return result;
            }
            catch (Exception ex)
            {
                _diagnostics.RecordError(_middlewareType, ex, true);
                throw;
            }
        });
    }

    /// <summary>
    /// Invokes the wrapped middleware after state reduction and records execution metrics.
    /// </summary>
    /// <param name="actions">The stream of action contexts to process.</param>
    /// <returns>The processed action contexts.</returns>
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        return actions.Select(context =>
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ActionContext result = _innerMiddleware.InvokeAfterReduce(Observable.Return(context))
                    .FirstOrDefaultAsync()
                    .GetAwaiter()
                    .GetResult();

                _diagnostics.RecordExecution(_middlewareType, stopwatch.Elapsed, false);
                return result;
            }
            catch (Exception ex)
            {
                _diagnostics.RecordError(_middlewareType, ex, false);
                throw;
            }
        });
    }
}

/// <summary>
/// Extension methods for enabling diagnostics.
/// </summary>
public static class DiagnosticExtensions
{
    /// <summary>
    /// Enables diagnostic collection for all middlewares.
    /// </summary>
    public static IStoreBuilder EnableDiagnostics(this IStoreBuilder builder)
    {
        // Register the diagnostics service
        builder.Services.AddSingleton<MiddlewareDiagnostics>();

        // Mark that diagnostics are enabled
        builder.Services.AddSingleton<DiagnosticsMarker>();

        return builder;
    }

    /// <summary>
    /// Gets the middleware diagnostics from the service provider.
    /// </summary>
    public static MiddlewareDiagnostics? GetMiddlewareDiagnostics(this IServiceProvider services)
    {
        return services.GetService<MiddlewareDiagnostics>();
    }
}

/// <summary>
/// Marker class to indicate that diagnostics are enabled.
/// </summary>
internal class DiagnosticsMarker
{
}
