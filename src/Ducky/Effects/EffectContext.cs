namespace Ducky;

/// <summary>What an effect run sees: the live state, the action that started it, the store's clock, and dispatch.</summary>
public sealed class EffectContext : IDispatcher
{
    private readonly Dispatcher _dispatcher;
    private readonly EffectRunToken _run;

    internal EffectContext(Dispatcher dispatcher, ActionContext trigger, EffectRunToken run)
    {
        _dispatcher = dispatcher;
        _run = run;
        Trigger = trigger;
    }

    // The run this context belongs to: EffectContextExtensions.Run classifies cancellation by its token (§5.5, §6.6).
    internal EffectRunToken Run => _run;

    /// <summary>Gets the latest snapshot: each read returns the state as it is now, not as it was when the run started.</summary>
    public StateSnapshot State => _dispatcher.State;

    /// <summary>Gets the action that started this run, as the store processed it.</summary>
    public ActionContext Trigger { get; }

    /// <summary>Gets the store's <see cref="TimeProvider"/>; use it for delays and timestamps, so tests can fake time.</summary>
    public TimeProvider Time => _dispatcher.Time;

    /// <inheritdoc cref="IDispatcher.Dispatch(object)" path="/summary"/>
    /// <param name="action">The action, dispatched with <see cref="Origin.Effect"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <remarks>
    /// Ignored, with a Debug log, once the store's disposal began; on a live store, dropped (Debug log) if this run was
    /// superseded (<see cref="Concurrency.Switch"/>), here or when the store processes it. A run exists only once the
    /// store materialized, so this never throws DUCKY353.
    /// </remarks>
    public void Dispatch(object action) => _dispatcher.DispatchFromRun(action, _run, null);

    /// <inheritdoc cref="IDispatcher.DispatchAsync(object)" path="/summary"/>
    /// <param name="action">The action, dispatched with <see cref="Origin.Effect"/>.</param>
    /// <returns>
    /// What happened to the action: <see cref="DispatchResult.Disposed"/> once the store's disposal began, and
    /// <see cref="DispatchResult.Dropped"/> on a live store if this run was superseded (<see cref="Concurrency.Switch"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <remarks>A run exists only once the store materialized, so this never throws DUCKY353.</remarks>
    public Task<DispatchResult> DispatchAsync(object action)
    {
        var completion = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.DispatchFromRun(action, _run, completion);
        return completion.Task;
    }
}
