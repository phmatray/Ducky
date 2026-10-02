namespace Ducky;

/// <content>The one privileged dispatch (SPEC §5.6, ADR-0032).</content>
public abstract partial class Middleware
{
    /// <summary>
    /// Dispatches <paramref name="action"/> with <see cref="Origin.System"/>: it bypasses the init buffer and every
    /// <see cref="MayDispatch"/> veto, and follows the normal causal depth rule (a dispatch from a hook is a synchronous
    /// child, so a loop closed through <see cref="AfterReduce"/> is dropped above <see cref="DuckyBuilder.MaxDispatchDepth"/>).
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="isFailure">
    /// <see langword="true"/> marks a failure action: a failure raised while handling it, or a synchronous descendant of
    /// it, is logged and never dispatched.
    /// </param>
    /// <remarks>
    /// Use it from middleware init: awaiting <see cref="IDispatcher.DispatchAsync(object)"/> of a user action there waits until the init
    /// timeout, because user actions are buffered until the store is ready.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The store has not attached itself yet (in the constructor).</exception>
    protected void DispatchSystem(object action, bool isFailure = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = Attached.Dispatcher;
        dispatcher.Enqueue(dispatcher.NewPending(action, Origin.System, null, isFailure));
    }
}
