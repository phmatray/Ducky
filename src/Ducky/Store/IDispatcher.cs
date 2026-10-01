namespace Ducky;

/// <summary>Dispatches actions to the store. Resolved from DI, it forwards to the <see cref="IStore"/> of the same scope.</summary>
public interface IDispatcher
{
    /// <summary>
    /// Enqueues <paramref name="action"/> and returns. Never throws for reducer failures. The action is reduced before this
    /// returns only if the caller became the drainer; use <see cref="DispatchAsync"/> for read-your-writes.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    void Dispatch(object action);

    /// <summary>
    /// Enqueues <paramref name="action"/>. The task completes, never inline on the drainer, once the action is reduced,
    /// vetoed, dropped or the store is disposed. It never faults and never cancels.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>What happened to the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    Task<DispatchResult> DispatchAsync(object action);
}
