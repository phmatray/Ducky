namespace Ducky;

/// <summary>
/// The base of every effect: derive from <see cref="Effect{TAction}"/>, or from <see cref="EffectGroup"/> for several
/// action types in one class. Register it with
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

    // New runners for each store that materializes this effect (§6.6): the store's effect index maps each exact action
    // type of a runner to it (ADR-0008).
    internal abstract EffectRunner[] CreateRunners();
}

/// <summary>Reacts to every action of the exact type <typeparamref name="TAction"/> once it is processed.</summary>
/// <typeparam name="TAction">The action type, matched exactly: a derived action type does not start this effect.</typeparam>
public abstract class Effect<TAction> : Effect
    where TAction : notnull
{
    // The wrapper of each registered case, by exact case type: null while the effect has no case.
    private Dictionary<Type, Func<object, TAction>>? _cases;
    private bool _frozen;

    /// <summary>Initializes a new instance of the <see cref="Effect{TAction}"/> class and registers its cases.</summary>
#pragma warning disable RS0022 // justification: by design (SPEC §5.5), Effect is derivable only through Effect<TAction> and EffectGroup
#pragma warning disable CA2214 // justification: by design (ADR-0051), the hook only calls OnCase<T>, which needs no state of the derived class
    protected Effect() => RegisterCases();
#pragma warning restore CA2214
#pragma warning restore RS0022

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

    /// <summary>
    /// Registers each case of a union <typeparamref name="TAction"/> with <see cref="OnCase{TCase}"/>, called once by the
    /// constructor. The Ducky generator overrides it in the effect's <c>partial</c> declaration; without the generator,
    /// override it by hand. An effect with no registered case handles <typeparamref name="TAction"/> itself.
    /// </summary>
    protected virtual void RegisterCases()
    {
    }

    /// <summary>
    /// Registers one case of a union <typeparamref name="TAction"/>, from <see cref="RegisterCases"/>. Every case shares
    /// this effect's <see cref="Policy"/>, <see cref="ConcurrencyKey"/> and slots.
    /// </summary>
    /// <typeparam name="TCase">The concrete case type; a derived type is not matched.</typeparam>
    /// <param name="wrap">Wraps the dispatched case in the union passed to <see cref="ConcurrencyKey"/> and <see cref="Handle"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="wrap"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// <typeparamref name="TCase"/> is a union (DUCKY319), is abstract, an interface or <see cref="object"/> (DUCKY307),
    /// or is already registered (DUCKY308).
    /// </exception>
    /// <exception cref="InvalidOperationException">Called after a store created this effect's runners.</exception>
    protected void OnCase<TCase>(Func<TCase, TAction> wrap)
        where TCase : notnull
    {
        ArgumentNullException.ThrowIfNull(wrap);
        var caseType = typeof(TCase);
        if (_frozen)
        {
            throw new InvalidOperationException(
                $"{GetType()} calls OnCase<{caseType}>() after a store created its runners. Call OnCase<T>() only from RegisterCases().");
        }

        if (typeof(System.Runtime.CompilerServices.IUnion).IsAssignableFrom(caseType))
        {
            throw new DuckyConfigurationException([DuckyErrors.UnionHandlerType(GetType(), caseType)]);
        }

        // Interfaces are abstract too.
        if (caseType.IsAbstract || caseType == typeof(object))
        {
            throw new DuckyConfigurationException([DuckyErrors.NonConcreteHandlerType(GetType(), caseType)]);
        }

        _cases ??= [];
        if (!_cases.TryAdd(caseType, action => wrap((TCase)action)))
        {
            throw new DuckyConfigurationException([DuckyErrors.DuplicateHandler(GetType(), caseType)]);
        }
    }

    // Policy is read once here, when the store builds the runner (§6.6). One runner serves every case, so the cases share
    // its slots.
    internal sealed override EffectRunner[] CreateRunners()
    {
        _frozen = true;
        if (_cases is not { } cases)
        {
            return [new(this, [typeof(TAction)], Policy, action => ConcurrencyKey((TAction)action), (action, context, token) => Handle((TAction)action, context, token))];
        }

        return [new(this, [.. cases.Keys], Policy, action => ConcurrencyKey(cases[action.GetType()](action)), (action, context, token) => Handle(cases[action.GetType()](action), context, token))];
    }
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

    /// <summary>
    /// While a run is in flight for a key, a new action for that key starts no run: its effect is dropped (the action
    /// itself is still reduced).
    /// </summary>
    Exhaust,

    /// <summary>
    /// Runs for one key never overlap: each starts when the previous one for that key has completed, in dispatch order.
    /// A run still waiting when the store is disposed never invokes its handler.
    /// </summary>
    Queue,
}
