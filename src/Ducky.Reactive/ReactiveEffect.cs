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
/// <para>
/// Never load a Ducky.Blazor <c>Prerender&lt;T&gt;</c> slice from a reactive effect: the prerender seed does not wait for
/// the pipeline, so it captures the slice's loading state and the interactive side takes it as loaded. Use an
/// <see cref="Effect{TAction}"/>.
/// </para>
/// <para>
/// Every failure (a throw from <see cref="Handle"/>, a non-terminal error, a completion with a failure) is logged and
/// dispatched as an <see cref="EffectFailed"/> whose <see cref="EffectFailed.ActionType"/> is <c>"reactive"</c>, with
/// <see cref="Origin.System"/>: never vetoed and never "unhandled". A non-terminal error leaves the pipeline running; a
/// failure that ends it resubscribes after <see cref="RetryDelay"/>, unless the store is disposed first.
/// </para>
/// <para>
/// A failure raised synchronously while the store handles a failure action (an <see cref="EffectFailed"/>, a
/// <see cref="ReducerFailed"/>, anything a middleware sends with <c>DispatchSystem(action, isFailure: true)</c>, or a
/// synchronous descendant of one) is logged only (INV-12). After a real asynchronous hop it starts a new chain and is
/// published, so a pipeline that reacts asynchronously to <see cref="EffectFailed"/> and can fail must leave reactive
/// failures out, <c>.Where(static f =&gt; f.ActionType != "reactive")</c>, or it sustains itself for as long as it fails.
/// </para>
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
    /// <returns>
    /// The delay, or <see langword="null"/> to stop resubscribing. A delay under one millisecond waits one, and a delay
    /// above about 49.7 days (the <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> maximum), or a throw,
    /// is a failure like any other, and stops resubscribing.
    /// </returns>
    protected internal virtual TimeSpan? RetryDelay(int attempt) =>
        attempt > 8 ? null : TimeSpan.FromSeconds(Math.Min(30, 1 << (attempt - 1)));
}
