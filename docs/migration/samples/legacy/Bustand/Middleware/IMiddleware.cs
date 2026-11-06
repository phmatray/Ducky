namespace Bustand.Middleware;

/// <summary>
/// Interface for synchronous middleware that intercepts store state changes.
/// Implement this interface to add cross-cutting concerns such as validation,
/// logging, or side effects to state changes.
/// </summary>
/// <typeparam name="TState">The state type managed by the store.</typeparam>
/// <remarks>
/// <para>
/// Middleware executes in registration order. Each middleware has the opportunity
/// to inspect or block state changes before they occur, and to react after changes complete.
/// </para>
/// <para>
/// <b>OnBeforeChange:</b> Runs before the state update is applied. Return <c>false</c>
/// to veto the state change and prevent it from being applied. This is useful for
/// validation or authorization checks.
/// </para>
/// <para>
/// <b>OnAfterChange:</b> Runs after the state update has been applied. Use this for
/// side effects such as logging, analytics, or synchronization with external systems.
/// Exceptions in OnAfterChange are logged but do not break the pipeline.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class LoggingMiddleware&lt;TState&gt; : IMiddleware&lt;TState&gt; where TState : class
/// {
///     public bool OnBeforeChange(MiddlewareContext&lt;TState&gt; context)
///     {
///         Console.WriteLine($"State changing from {context.OldState} to {context.NewState}");
///         return true; // Allow the change
///     }
///
///     public void OnAfterChange(MiddlewareContext&lt;TState&gt; context)
///     {
///         Console.WriteLine($"State changed at {context.Timestamp}");
///     }
/// }
/// </code>
/// </example>
public interface IMiddleware<TState> where TState : class
{
    /// <summary>
    /// Called before a state change is applied.
    /// </summary>
    /// <param name="context">The context containing old state, new state, and metadata.</param>
    /// <returns>
    /// <c>true</c> to allow the state change to proceed;
    /// <c>false</c> to block the state change and prevent it from being applied.
    /// </returns>
    /// <remarks>
    /// If any middleware returns <c>false</c>, the state change is cancelled and
    /// no subsequent middleware will be called.
    /// </remarks>
    bool OnBeforeChange(MiddlewareContext<TState> context);

    /// <summary>
    /// Called after a state change has been applied.
    /// </summary>
    /// <param name="context">The context containing old state, new state, and metadata.</param>
    /// <remarks>
    /// <para>
    /// This method is called for side effects only. Exceptions thrown here are
    /// logged using <see cref="System.Diagnostics.Debug"/> and do not break the
    /// middleware pipeline.
    /// </para>
    /// <para>
    /// All middleware OnAfterChange methods are called even if one throws.
    /// </para>
    /// </remarks>
    void OnAfterChange(MiddlewareContext<TState> context);
}

/// <summary>
/// Interface for asynchronous middleware that intercepts store state changes.
/// Use this interface when middleware needs to perform async operations such as
/// network calls or database access.
/// </summary>
/// <typeparam name="TState">The state type managed by the store.</typeparam>
/// <remarks>
/// <para>
/// Async middleware follows the same execution semantics as <see cref="IMiddleware{TState}"/>
/// but allows for async operations within the middleware methods.
/// </para>
/// <para>
/// <b>Important:</b> Async middleware is awaited sequentially in registration order.
/// Long-running operations will delay state change propagation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class AuditMiddleware&lt;TState&gt; : IAsyncMiddleware&lt;TState&gt; where TState : class
/// {
///     private readonly IAuditService _audit;
///
///     public AuditMiddleware(IAuditService audit) => _audit = audit;
///
///     public Task&lt;bool&gt; OnBeforeChangeAsync(MiddlewareContext&lt;TState&gt; context)
///     {
///         return Task.FromResult(true); // Allow all changes
///     }
///
///     public async Task OnAfterChangeAsync(MiddlewareContext&lt;TState&gt; context)
///     {
///         await _audit.LogChangeAsync(context.StoreType.Name, context.Timestamp);
///     }
/// }
/// </code>
/// </example>
public interface IAsyncMiddleware<TState> where TState : class
{
    /// <summary>
    /// Called before a state change is applied.
    /// </summary>
    /// <param name="context">The context containing old state, new state, and metadata.</param>
    /// <returns>
    /// A task that resolves to <c>true</c> to allow the state change to proceed;
    /// <c>false</c> to block the state change and prevent it from being applied.
    /// </returns>
    /// <remarks>
    /// If any middleware returns <c>false</c>, the state change is cancelled and
    /// no subsequent middleware will be called.
    /// </remarks>
    Task<bool> OnBeforeChangeAsync(MiddlewareContext<TState> context);

    /// <summary>
    /// Called after a state change has been applied.
    /// </summary>
    /// <param name="context">The context containing old state, new state, and metadata.</param>
    /// <returns>A task that completes when the middleware has finished its work.</returns>
    /// <remarks>
    /// <para>
    /// This method is called for side effects only. Exceptions thrown here are
    /// logged and do not break the middleware pipeline.
    /// </para>
    /// <para>
    /// All middleware OnAfterChangeAsync methods are called even if one throws.
    /// </para>
    /// </remarks>
    Task OnAfterChangeAsync(MiddlewareContext<TState> context);
}
