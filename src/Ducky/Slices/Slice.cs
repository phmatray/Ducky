using System.Collections.Frozen;
using System.Text.Json;

namespace Ducky;

/// <summary>
/// A named slice of the store's state. Derive from <see cref="Slice{TState}"/>.
/// </summary>
public abstract class Slice
{
    private string? _key;

    private protected Slice()
    {
    }

    /// <summary>
    /// Gets the slice key: by default derived from the slice type name (<c>TodoSlice</c> gives <c>todo</c>) and cached.
    /// A generic slice must override it.
    /// </summary>
    public virtual string Key => _key ??= SliceKey.FromType(GetType());

    /// <summary>Gets the declared state type of the slice.</summary>
    public abstract Type StateType { get; }

    // Read once; published to other packages through IStore.InitialState.
    internal abstract object InitialState { get; }

    // The store freezes the handler table at build (SPEC §6.1); CanHandle and TryReduce read the frozen table only.
    internal abstract void Freeze();

    internal abstract bool CanHandle(Type actionType);

    internal abstract bool TryReduce(object state, object action, out object next);

    // A restored value (SPEC §6.4 step 6): taken when it is the declared state type, or a JsonElement deserialized with the
    // declared state type's JsonTypeInfo (§10); a failure is logged with the slice key.
    internal abstract bool TryRestore(object value, DuckyJson json, out object state);

    // The store attaches itself to the slice instances it owns when it is built (§12); only a SliceStore keeps it.
    internal virtual void Attach(DuckyStore store)
    {
    }
}

/// <summary>
/// A slice holding a <typeparamref name="TState"/>, reduced by the handlers its constructor registers with
/// <c>On&lt;TAction&gt;</c>. A handler matches its exact action type only, never a derived one.
/// </summary>
/// <typeparam name="TState">The state type; a reference type, since change detection is by reference.</typeparam>
public abstract class Slice<TState> : Slice
    where TState : class
{
    private readonly Dictionary<Type, Func<TState, object, TState>> _handlers = [];
    private FrozenDictionary<Type, Func<TState, object, TState>>? _frozen;
    private TState? _initial;

    /// <summary>Initializes a new instance of the <see cref="Slice{TState}"/> class.</summary>
#pragma warning disable RS0022 // justification: by design (SPEC §5.3), Slice<TState> is the one inheritable form of Slice
    protected Slice()
#pragma warning restore RS0022
    {
    }

    /// <summary>Gets <typeparamref name="TState"/>: the declared state type, never the runtime type of a state.</summary>
    public sealed override Type StateType => typeof(TState);

    /// <summary>Gets the initial state, read once when the store is built and then cached.</summary>
    protected abstract TState Initial { get; }

    internal override object InitialState => _initial ??= Initial;

    /// <summary>Registers the reducer of <typeparamref name="TAction"/>, from the slice constructor.</summary>
    /// <typeparam name="TAction">The concrete action type; a derived type is not matched.</typeparam>
    /// <param name="reducer">Returns the next state; return the same instance when nothing changed.</param>
    /// <exception cref="DuckyConfigurationException">
    /// <typeparamref name="TAction"/> already has a handler (DUCKY308), or is abstract, an interface or
    /// <see cref="object"/> (DUCKY307).
    /// </exception>
    /// <exception cref="InvalidOperationException">The store already froze the slice.</exception>
    protected void On<TAction>(Func<TState, TAction, TState> reducer)
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Register(typeof(TAction), (state, action) => reducer(state, (TAction)action));
    }

    /// <summary>Registers the reducer of <typeparamref name="TAction"/> for a reducer that ignores the action's content.</summary>
    /// <typeparam name="TAction">The concrete action type; a derived type is not matched.</typeparam>
    /// <param name="reducer">Returns the next state; return the same instance when nothing changed.</param>
    /// <exception cref="DuckyConfigurationException">
    /// <typeparamref name="TAction"/> already has a handler (DUCKY308), or is abstract, an interface or
    /// <see cref="object"/> (DUCKY307).
    /// </exception>
    /// <exception cref="InvalidOperationException">The store already froze the slice.</exception>
    protected void On<TAction>(Func<TState, TState> reducer)
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Register(typeof(TAction), (state, _) => reducer(state));
    }

    internal override void Freeze() => _frozen = _handlers.ToFrozenDictionary();

    internal override bool CanHandle(Type actionType) => _frozen!.ContainsKey(actionType);

    // Exact match on action.GetType() (ADR-0008).
    internal override bool TryReduce(object state, object action, out object next)
    {
        if (_frozen!.TryGetValue(action.GetType(), out var reducer))
        {
            next = reducer((TState)state, action);
            return true;
        }

        next = state;
        return false;
    }

    internal override bool TryRestore(object value, DuckyJson json, out object state)
    {
        if (value is JsonElement element)
        {
            // JSON null deserializes, but is no state: never ignored silently either (§10).
            if (json.TryDeserialize(element, typeof(TState), out var read, Key) && read is null)
            {
                json.LogUndeserializable(null, Key, typeof(TState));
            }

            value = read!;
        }

        state = value;
        return value is TState;
    }

    // DUCKY307/DUCKY308 come from the DuckyErrors catalogue (SPEC §8.3); AddSlice reads them from Errors (§5.1, §6.1).
    private void Register(Type actionType, Func<TState, object, TState> reducer)
    {
        if (_frozen is not null)
        {
            throw new InvalidOperationException(
                $"Slice {GetType()} registers On<{actionType}> after the store froze it. Register every handler in the slice constructor.");
        }

        // Interfaces are abstract too.
        if (actionType.IsAbstract || actionType == typeof(object))
        {
            throw new DuckyConfigurationException([DuckyErrors.NonConcreteHandlerType(GetType(), actionType)]);
        }

        if (!_handlers.TryAdd(actionType, reducer))
        {
            throw new DuckyConfigurationException([DuckyErrors.DuplicateHandler(GetType(), actionType)]);
        }
    }
}
