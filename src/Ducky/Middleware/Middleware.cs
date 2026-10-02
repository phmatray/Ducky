namespace Ducky;

/// <summary>
/// Observes and gates the actions a store processes. Register it with <see cref="DuckyBuilder.Use{TMiddleware}"/>: the
/// store creates it, owns it and disposes it.
/// </summary>
/// <remarks>
/// The store may call <see cref="BeforeReduce"/> and <see cref="AfterReduce"/> before this middleware's init has started
/// and concurrently with the asynchronous part of its init, so state shared between the hooks and init must be
/// thread-safe, and code that runs after the init token was cancelled must not restore or dispatch stale data.
/// </remarks>
public abstract partial class Middleware : IAsyncDisposable
{
    private DuckyStore? _store;

    /// <summary>Initializes a new instance of the <see cref="Middleware"/> class.</summary>
    protected Middleware()
    {
    }

    /// <summary>Gets the store this middleware belongs to.</summary>
    /// <exception cref="InvalidOperationException">The store has not attached itself yet (in the constructor).</exception>
    protected IStore Store => Attached;

    /// <summary>
    /// Gets how long the store waits for <see cref="DisposeAsync"/>: <see cref="DuckyBuilder.DisposeTimeout"/>, clamped to
    /// a bound <see cref="Task.WaitAsync(TimeSpan)"/> accepts (a negative value becomes zero). Bound any flush by it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The store has not attached itself yet (in the constructor).</exception>
    protected TimeSpan DisposeTimeout => Attached.Dispatcher.DisposeTimeout;

    private DuckyStore Attached => _store
        ?? throw new InvalidOperationException("The store attaches Store and DisposeTimeout after the constructor returns; use them from InitializeAsync or a hook.");

    /// <summary>
    /// Decides whether an action is processed. Consulted only for <see cref="Origin.Local"/> and <see cref="Origin.Effect"/>
    /// actions, in registration order: the first <see langword="false"/> completes the action
    /// <see cref="DispatchResult.Vetoed"/>, and a throw completes it <see cref="DispatchResult.Failed"/>.
    /// </summary>
    /// <param name="context">The action, before the reduce.</param>
    /// <returns><see langword="false"/> to veto the action.</returns>
    public virtual bool MayDispatch(ActionContext context) => true;

    /// <summary>Runs before the reduce, in registration order. A throw fails the action and commits nothing.</summary>
    /// <param name="context">The action, before the reduce.</param>
    public virtual void BeforeReduce(ActionContext context)
    {
    }

    /// <summary>Runs after the reduce, in registration order. A throw is logged and the other middleware still run.</summary>
    /// <param name="context">The action, with the state it committed.</param>
    public virtual void AfterReduce(ActionContext context)
    {
    }

    /// <summary>
    /// Called once by the store's disposal, after its drain exited, in reverse registration order. A throw is logged and
    /// the other middleware are still disposed.
    /// </summary>
    /// <returns>A task that completes when this middleware is disposed.</returns>
#pragma warning disable CA1816 // justification: the store owns middleware and calls DisposeAsync once; a derived finalizer is its own concern
    public virtual ValueTask DisposeAsync() => default;
#pragma warning restore CA1816

    // Called by the store when it creates this middleware, before any hook or init runs.
    internal void Attach(DuckyStore store) => _store = store;
}
