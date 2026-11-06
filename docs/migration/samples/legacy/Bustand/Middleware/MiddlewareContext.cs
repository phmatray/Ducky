namespace Bustand.Middleware;

/// <summary>
/// Immutable context object containing all information about a state change.
/// Passed to middleware during BeforeChange and AfterChange hooks.
/// </summary>
/// <typeparam name="TState">The state type managed by the store.</typeparam>
/// <remarks>
/// <para>
/// This record provides middleware with full context about the state change,
/// enabling informed decisions about whether to allow or block changes,
/// and comprehensive logging/auditing capabilities.
/// </para>
/// <para>
/// <b>Properties:</b>
/// <list type="bullet">
/// <item><see cref="OldState"/>: The state before the change (for comparison)</item>
/// <item><see cref="NewState"/>: The state after the change (what will be applied)</item>
/// <item><see cref="StoreType"/>: The concrete store type for identification</item>
/// <item><see cref="ActionName"/>: Optional name of the action that triggered the change</item>
/// <item><see cref="Timestamp"/>: When the change occurred (for audit trails)</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public bool OnBeforeChange(MiddlewareContext&lt;MyState&gt; context)
/// {
///     // Compare old and new states
///     if (context.OldState.Count > context.NewState.Count)
///     {
///         Console.WriteLine($"Count decreased in {context.StoreType.Name}");
///     }
///
///     // Log with timestamp
///     Console.WriteLine($"[{context.Timestamp:HH:mm:ss}] {context.ActionName ?? "unknown action"}");
///
///     return true;
/// }
/// </code>
/// </example>
public sealed record MiddlewareContext<TState> where TState : class
{
    /// <summary>
    /// Gets the state before the change was applied.
    /// </summary>
    /// <remarks>
    /// Use this to compare with <see cref="NewState"/> to determine what changed,
    /// or to implement undo/redo functionality.
    /// </remarks>
    public required TState OldState { get; init; }

    /// <summary>
    /// Gets the state after the change is applied.
    /// </summary>
    /// <remarks>
    /// In <see cref="IMiddleware{TState}.OnBeforeChange"/>, this represents the proposed
    /// new state that will be applied if middleware allows the change.
    /// In <see cref="IMiddleware{TState}.OnAfterChange"/>, this is the state that was just applied.
    /// </remarks>
    public required TState NewState { get; init; }

    /// <summary>
    /// Gets the concrete type of the store where the change occurred.
    /// </summary>
    /// <remarks>
    /// Use this to identify which store triggered the middleware,
    /// useful when a single middleware is registered for multiple stores.
    /// </remarks>
    public required Type StoreType { get; init; }

    /// <summary>
    /// Gets the optional name of the action that triggered the state change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is <c>null</c> when the action name was not provided.
    /// Action names are typically method names like "Increment" or "SetUser".
    /// </para>
    /// <para>
    /// For debugging and logging, an action name helps identify what operation
    /// caused the state change.
    /// </para>
    /// </remarks>
    public string? ActionName { get; init; }

    /// <summary>
    /// Gets the timestamp when the state change occurred.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="DateTimeOffset"/> for timezone-aware timestamps,
    /// making it suitable for logging, auditing, and time-travel debugging.
    /// </remarks>
    public required DateTimeOffset Timestamp { get; init; }
}
