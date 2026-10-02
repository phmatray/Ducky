namespace Ducky;

/// <summary>What an effect run sees: the live state, the action that started it, the store's clock, and dispatch.</summary>
public sealed class EffectContext : IDispatcher
{
    private readonly Dispatcher _dispatcher;

    internal EffectContext(Dispatcher dispatcher, ActionContext trigger)
    {
        _dispatcher = dispatcher;
        Trigger = trigger;
    }

    /// <summary>Gets the latest snapshot: each read returns the state as it is now, not as it was when the run started.</summary>
    public StateSnapshot State => _dispatcher.State;

    /// <summary>Gets the action that started this run, as the store processed it.</summary>
    public ActionContext Trigger { get; }

    /// <summary>Gets the store's <see cref="TimeProvider"/>; use it for delays and timestamps, so tests can fake time.</summary>
    public TimeProvider Time => _dispatcher.Time;

    /// <inheritdoc cref="IDispatcher.Dispatch(object)" path="/summary"/>
    /// <param name="action">The action, dispatched with <see cref="Origin.Effect"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <remarks>A run exists only once the store materialized, so this never throws DUCKY353.</remarks>
    public void Dispatch(object action) => _dispatcher.Dispatch(action, Origin.Effect);

    /// <inheritdoc cref="IDispatcher.DispatchAsync(object)" path="/summary"/>
    /// <param name="action">The action, dispatched with <see cref="Origin.Effect"/>.</param>
    /// <returns>What happened to the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <remarks>A run exists only once the store materialized, so this never throws DUCKY353.</remarks>
    public Task<DispatchResult> DispatchAsync(object action) => _dispatcher.DispatchAsync(action, Origin.Effect);
}
