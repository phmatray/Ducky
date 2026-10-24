using R3;

namespace Ducky.Reactive;

/// <summary>
/// An effect written as an R3 pipeline over the store's actions and state. Register it with
/// <see cref="DuckyReactiveExtensions.AddReactiveEffect{TEffect}"/>: the store creates it from its scope, owns it and
/// disposes it. Inject <see cref="TimeProvider"/> for time operators, so tests can fake time.
/// </summary>
/// <remarks>
/// Reactive work is excluded from quiescence: <see cref="IStore.WhenIdleAsync"/> never waits for a pipeline, even one
/// with a request in flight, so test the observable outcome (advance a fake clock, then await a dispatch) instead.
/// </remarks>
public abstract class ReactiveEffect
{
    /// <summary>Initializes a new instance of the <see cref="ReactiveEffect"/> class.</summary>
    protected ReactiveEffect()
    {
    }

    /// <summary>
    /// Builds the pipeline, subscribed once when the store starts. Every action it emits is dispatched with
    /// <see cref="Origin.Effect"/>: an emission inside the processing of an action is a synchronous child of it.
    /// </summary>
    /// <param name="actions">
    /// <see cref="StoreInitialized"/> once, then every action the store processes; filter it with
    /// <see cref="DuckyReactiveExtensions.OfActionType{TAction}"/>.
    /// </param>
    /// <param name="state">The state: replays the current snapshot on subscribe (never null), then each change.</param>
    /// <returns>The actions to dispatch.</returns>
    public abstract Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state);

    /// <summary>
    /// Gets how long to wait before resubscribing after the pipeline completed with a failure. Defaults to 1, 2, 4, 8,
    /// 16 then 30 seconds, and stops after the eighth attempt.
    /// </summary>
    /// <param name="attempt">The resubscribe attempt, from 1; it resets after a successful emission.</param>
    /// <returns>The delay, or <see langword="null"/> to stop resubscribing.</returns>
    protected internal virtual TimeSpan? RetryDelay(int attempt) =>
        attempt > 8 ? null : TimeSpan.FromSeconds(Math.Min(30, 1 << (attempt - 1)));
}
