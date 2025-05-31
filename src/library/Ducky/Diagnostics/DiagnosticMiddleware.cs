using System.Diagnostics;
using Ducky.Builder;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Diagnostics;

/// <summary>
/// A middleware that wraps other middlewares to collect diagnostic information.
/// </summary>
public class DiagnosticMiddleware : IMiddleware
{
    private readonly IMiddleware _innerMiddleware;
    private readonly MiddlewareDiagnostics _diagnostics;
    private readonly Type _middlewareType;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticMiddleware"/> class.
    /// </summary>
    /// <param name="innerMiddleware">The middleware to wrap and collect diagnostics for.</param>
    /// <param name="diagnostics">The diagnostics service to record metrics.</param>
    public DiagnosticMiddleware(
        IMiddleware innerMiddleware,
        MiddlewareDiagnostics diagnostics)
    {
        _innerMiddleware = innerMiddleware;
        _diagnostics = diagnostics;
        _middlewareType = innerMiddleware.GetType();
    }

    /// <inheritdoc />
    public async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        await _innerMiddleware.InitializeAsync(dispatcher, store).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void AfterInitializeAllMiddlewares()
    {
        _innerMiddleware.AfterInitializeAllMiddlewares();
    }

    /// <inheritdoc />
    public bool MayDispatchAction(object action)
        => ExecuteWithDiagnostics(() => _innerMiddleware.MayDispatchAction(action), isBeforePhase: true);

    /// <inheritdoc />
    public void BeforeDispatch(object action)
        => ExecuteWithDiagnostics(() => _innerMiddleware.BeforeDispatch(action), isBeforePhase: true);

    /// <inheritdoc />
    public void AfterDispatch(object action)
        => ExecuteWithDiagnostics(() => _innerMiddleware.AfterDispatch(action), isBeforePhase: false);
    
    private void ExecuteWithDiagnostics(Action action, bool isBeforePhase)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            action();
            _diagnostics.RecordExecution(_middlewareType, stopwatch.Elapsed, isBeforePhase);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(_middlewareType, ex, isBeforePhase);
            throw;
        }
    }
    
    private T ExecuteWithDiagnostics<T>(Func<T> func, bool isBeforePhase)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            T result = func();
            _diagnostics.RecordExecution(_middlewareType, stopwatch.Elapsed, isBeforePhase);
            return result;
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(_middlewareType, ex, isBeforePhase);
            throw;
        }
    }

    /// <inheritdoc />
    public IDisposable BeginInternalMiddlewareChange()
    {
        return _innerMiddleware.BeginInternalMiddlewareChange();
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
