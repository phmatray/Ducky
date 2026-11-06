using System.Diagnostics;

namespace Bustand.Middleware;

/// <summary>
/// Executes a chain of middleware for store state changes.
/// Middleware is invoked in registration order (FIFO).
/// </summary>
/// <typeparam name="TState">The state type managed by the store.</typeparam>
/// <remarks>
/// <para>
/// The pipeline manages the execution flow of middleware during state changes:
/// <list type="bullet">
/// <item><b>BeforeChange:</b> All middleware are consulted in order; any can veto the change</item>
/// <item><b>AfterChange:</b> All middleware are notified in order; exceptions are logged but don't break the chain</item>
/// </list>
/// </para>
/// <para>
/// <b>Error handling:</b>
/// <list type="bullet">
/// <item>OnBeforeChange exceptions bubble up to the caller (validation failures must be visible)</item>
/// <item>OnAfterChange exceptions are logged via <see cref="Debug"/> and pipeline continues</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var middleware = new List&lt;IMiddleware&lt;MyState&gt;&gt; { new LoggingMiddleware(), new ValidationMiddleware() };
/// var pipeline = new MiddlewarePipeline&lt;MyState&gt;(middleware);
///
/// var context = new MiddlewareContext&lt;MyState&gt;
/// {
///     OldState = currentState,
///     NewState = newState,
///     StoreType = typeof(MyStore),
///     Timestamp = DateTimeOffset.UtcNow
/// };
///
/// if (pipeline.InvokeBeforeChange(context))
/// {
///     // Apply state change
///     pipeline.InvokeAfterChange(context);
/// }
/// </code>
/// </example>
public sealed class MiddlewarePipeline<TState> where TState : class
{
    private readonly IReadOnlyList<IMiddleware<TState>> _middleware;

    /// <summary>
    /// Gets an empty pipeline instance that allows all state changes.
    /// </summary>
    /// <remarks>
    /// Use this when no middleware is configured to avoid null checks and allocations.
    /// </remarks>
    internal static MiddlewarePipeline<TState> Empty { get; } = new([]);

    /// <summary>
    /// Creates a new middleware pipeline with the specified middleware.
    /// </summary>
    /// <param name="middleware">
    /// The middleware to execute, in registration order.
    /// Pass an empty collection for a no-op pipeline.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when middleware is null.</exception>
    public MiddlewarePipeline(IEnumerable<IMiddleware<TState>> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middleware = middleware.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the number of middleware in this pipeline.
    /// </summary>
    public int Count => _middleware.Count;

    /// <summary>
    /// Invokes all middleware BeforeChange hooks in registration order.
    /// </summary>
    /// <param name="context">The context describing the state change.</param>
    /// <returns>
    /// <c>true</c> if all middleware allow the change;
    /// <c>false</c> if any middleware vetoes the change.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    /// <remarks>
    /// <para>
    /// Middleware are called in registration order. If any middleware returns <c>false</c>,
    /// iteration stops immediately and <c>false</c> is returned.
    /// </para>
    /// <para>
    /// <b>Exception handling:</b> If a middleware's OnBeforeChange throws an exception,
    /// it bubbles up to the caller. This is intentional - validation failures should
    /// be visible to the code attempting the state change.
    /// </para>
    /// </remarks>
    public bool InvokeBeforeChange(MiddlewareContext<TState> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var middleware in _middleware)
        {
            try
            {
                if (!middleware.OnBeforeChange(context))
                {
                    // Middleware vetoed the change
                    return false;
                }
            }
            catch (Exception ex)
            {
                // BeforeChange exceptions bubble up - caller needs to know validation failed
                Debug.WriteLine($"[Bustand] Middleware {middleware.GetType().Name}.OnBeforeChange threw: {ex.Message}");
                throw;
            }
        }

        return true;
    }

    /// <summary>
    /// Invokes all middleware AfterChange hooks in registration order.
    /// </summary>
    /// <param name="context">The context describing the state change.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    /// <remarks>
    /// <para>
    /// All middleware are called regardless of exceptions. If a middleware's OnAfterChange
    /// throws, the exception is logged via <see cref="Debug.WriteLine"/> and the next
    /// middleware is called.
    /// </para>
    /// <para>
    /// This ensures that logging, analytics, and other side effects don't break
    /// each other when one fails.
    /// </para>
    /// </remarks>
    public void InvokeAfterChange(MiddlewareContext<TState> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var middleware in _middleware)
        {
            try
            {
                middleware.OnAfterChange(context);
            }
            catch (Exception ex)
            {
                // AfterChange exceptions are logged but don't break the pipeline
                Debug.WriteLine($"[Bustand] Middleware {middleware.GetType().Name}.OnAfterChange threw: {ex.Message}");
                // Continue to next middleware
            }
        }
    }
}
