namespace Ducky;

/// <summary>
/// The base of every effect: derive from <see cref="Effect{TAction}"/>. Register it with
/// <see cref="DuckyBuilder.AddEffect{TEffect}()"/>: the store creates it on its first use, owns it and disposes it.
/// </summary>
public abstract class Effect
{
    private protected Effect()
    {
    }

    /// <summary>
    /// Gets a value indicating whether runs of this effect are long-lived (pollers, listeners), which
    /// <see cref="IStore.WhenIdleAsync"/> does not wait for. Defaults to <see langword="false"/>.
    /// </summary>
    public virtual bool LongRunning => false;

    // The exact action type the store's effect index maps to this effect (ADR-0008).
    internal abstract Type ActionType { get; }

    internal abstract Task RunAsync(object action, EffectContext context, CancellationToken cancellationToken);

    // The registration's policy, read once when the store builds its EffectRunner (§6.6).
    internal abstract Concurrency RunPolicy { get; }

    // The run's concurrency key: user code, which the runner calls inside the run's try/catch (§6.6).
    internal abstract object? KeyOf(object action);
}

/// <summary>Reacts to every action of the exact type <typeparamref name="TAction"/> once it is processed.</summary>
/// <typeparam name="TAction">The action type, matched exactly: a derived action type does not start this effect.</typeparam>
public abstract class Effect<TAction> : Effect
    where TAction : notnull
{
    /// <summary>Initializes a new instance of the <see cref="Effect{TAction}"/> class.</summary>
#pragma warning disable RS0022 // justification: by design (SPEC §5.5), Effect is derivable only through Effect<TAction> and EffectGroup
    protected Effect()
#pragma warning restore RS0022
    {
    }

    /// <summary>Gets how runs of this effect relate to each other. Defaults to <see cref="Concurrency.Merge"/>.</summary>
    public virtual Concurrency Policy => Concurrency.Merge;

    /// <summary>
    /// Gets the key that groups the runs <see cref="Policy"/> relates: runs relate only to runs with an equal key.
    /// Defaults to <see langword="null"/>, one global slot for the effect.
    /// </summary>
    /// <param name="action">The action that starts the run.</param>
    /// <returns>The key, which needs value equality (DUCKY013); <see langword="null"/> for one global slot.</returns>
    protected virtual object? ConcurrencyKey(TAction action) => null;

    /// <summary>
    /// Handles one action. It starts on the draining thread and runs there until its first real <see langword="await"/>.
    /// An <see cref="OperationCanceledException"/> for <paramref name="cancellationToken"/> is cancellation; any other
    /// exception dispatches <see cref="EffectFailed"/>.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="context">The live state, the action's context, the clock, and dispatch.</param>
    /// <param name="cancellationToken">Cancelled when the store is disposed.</param>
    /// <returns>A task that completes when the run ends.</returns>
#pragma warning disable VSTHRD200 // justification: Handle is the normative name of SPEC §5.5
    public abstract Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken);
#pragma warning restore VSTHRD200

    internal sealed override Type ActionType => typeof(TAction);

    internal sealed override Task RunAsync(object action, EffectContext context, CancellationToken cancellationToken) =>
        Handle((TAction)action, context, cancellationToken);

    internal sealed override Concurrency RunPolicy => Policy;

    internal sealed override object? KeyOf(object action) => ConcurrencyKey((TAction)action);
}

/// <summary>How the runs of one effect relate to each other.</summary>
public enum Concurrency
{
    /// <summary>Every action starts a run, concurrently with the runs already in flight.</summary>
    Merge,

    /// <summary>
    /// A new action cancels the run in flight for the same key, and that superseded run can no longer dispatch
    /// (<see cref="DispatchResult.Dropped"/>): only the latest run per key publishes its result.
    /// </summary>
    Switch,
}
