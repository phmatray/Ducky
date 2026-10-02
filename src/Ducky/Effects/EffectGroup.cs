namespace Ducky;

/// <summary>
/// Several effects in one class: its constructor calls <see cref="On{TAction}"/> once per action type, each with its own
/// policy and key. Register it with <see cref="DuckyBuilder.AddEffect{TEffect}()"/>, like any effect.
/// </summary>
public abstract class EffectGroup : Effect
{
    // One runner factory per action type: each store that materializes the group creates its own runners (§6.6).
    private readonly Dictionary<Type, Func<EffectRunner>> _handlers = [];
    private bool _frozen;

    /// <summary>Initializes a new instance of the <see cref="EffectGroup"/> class.</summary>
#pragma warning disable RS0022 // justification: by design (SPEC §5.5), Effect is derivable only through Effect<TAction> and EffectGroup
    protected EffectGroup()
#pragma warning restore RS0022
    {
    }

    /// <summary>
    /// Registers the handler of <typeparamref name="TAction"/>, from the constructor. Each handler is its own effect: its
    /// runs relate only to each other, through <paramref name="policy"/> and <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TAction">The concrete action type; a derived type is not matched.</typeparam>
    /// <param name="handler">
    /// Handles one action, like <see cref="Effect{TAction}.Handle"/>: an <see cref="OperationCanceledException"/> for its
    /// token is cancellation; any other exception dispatches <see cref="EffectFailed"/>.
    /// </param>
    /// <param name="policy">How the runs of this handler relate to each other. Defaults to <see cref="Concurrency.Merge"/>.</param>
    /// <param name="key">
    /// The key that groups the runs <paramref name="policy"/> relates, which needs value equality (DUCKY013);
    /// <see langword="null"/>, or a function returning <see langword="null"/>, for one global slot.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// <typeparamref name="TAction"/> already has a handler (DUCKY308), or is abstract, an interface or
    /// <see cref="object"/> (DUCKY307). The store reports it as DUCKY353 at its first use.
    /// </exception>
    /// <exception cref="InvalidOperationException">Called after a store created this group's runners.</exception>
    protected void On<TAction>(
        Func<TAction, EffectContext, CancellationToken, Task> handler,
        Concurrency policy = Concurrency.Merge,
        Func<TAction, object?>? key = null)
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        var actionType = typeof(TAction);
        if (_frozen)
        {
            throw new InvalidOperationException(
                $"{GetType()} calls On<{actionType}>() after a store created its runners. Call On<T>() only from the constructor.");
        }

        // Interfaces are abstract too.
        if (actionType.IsAbstract || actionType == typeof(object))
        {
            throw new DuckyConfigurationException([DuckyErrors.NonConcreteHandlerType(GetType(), actionType)]);
        }

        if (!_handlers.TryAdd(actionType, () => new(this, actionType, policy, action => key?.Invoke((TAction)action), (action, context, token) => handler((TAction)action, context, token))))
        {
            throw new DuckyConfigurationException([DuckyErrors.DuplicateHandler(GetType(), actionType)]);
        }
    }

    internal sealed override EffectRunner[] CreateRunners()
    {
        _frozen = true;
        return [.. _handlers.Values.Select(create => create())];
    }
}
