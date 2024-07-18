using System.Diagnostics;
using System.Text.RegularExpressions;
using R3;
using R3dux.Exceptions;

namespace R3dux;

/// <summary>
/// Represents a strongly-typed state slice with state management and reducers.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this slice.</typeparam>
/// <remarks>
/// Each "slice reducer" is responsible for providing an initial value
/// and calculating the updates to that slice of the state.
/// </remarks>
public abstract partial record SliceReducers<TState> : ISlice<TState>
{
    private bool _isInitialized;
    private readonly ReactiveProperty<TState> _state = new();
    private readonly Subject<Unit> _stateUpdated = new();
    private readonly StateLoggerObserver<TState> _stateLoggerObserver = new();

    /// <inheritdoc />
    public virtual Observable<TState> State => _state;

    /// <inheritdoc />
    public virtual Observable<Unit> StateUpdated => _stateUpdated;

    /// <summary>
    /// A dictionary that holds the reducers mapped by the type of action.
    /// </summary>
    public Dictionary<Type, Func<TState, IAction, TState>> Reducers { get; } = new();

    /// <inheritdoc />
    public virtual string GetKey()
    {
        // get type of the inheriting class
        string typeName = GetType().Name;

        // the key should not end with "Reducers" or "Reducer"
        if (typeName.EndsWith("Reducers"))
        {
            typeName = typeName[..^8];
        }
        else if (typeName.EndsWith("Reducer"))
        {
            typeName = typeName[..^7];
        }

        // convert to kebab case
        typeName = LowerCharUpperCharRegex().Replace(typeName, "$1-$2");

        // return the key in lowercase
        return typeName.ToLower();
    }

    /// <inheritdoc />
    public virtual Type GetStateType()
        => typeof(TState);

    /// <inheritdoc />
    public virtual object GetState()
    {
        if (!_isInitialized)
        {
            _state.OnNext(GetInitialState());
            _isInitialized = true;
        }

        if (_state.Value is null)
        {
            throw new R3duxException("State is null.");
        }

        return _state.Value;
    }

    /// <inheritdoc />
    public void OnDispatch(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        var prevState = (TState)GetState();
        var updatedState = Reduce(prevState, action);
        stopwatch.Stop();

        if (prevState?.Equals(updatedState) == true)
        {
            return;
        }

        // First update the state...
        _state.OnNext(updatedState);

        // ...then notify subscribers that the state has been updated.
        _stateUpdated.OnNext(Unit.Default);

        var stateChange = new StateChange<TState>(
            action,
            prevState,
            updatedState,
            stopwatch.Elapsed.TotalMilliseconds);

        _stateLoggerObserver.OnNext(stateChange);
    }

    /// <summary>
    /// Maps a reducer function to a specific action type.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="reducer">The reducer function that takes the state and action and returns a new state.</param>
    /// <exception cref="ArgumentNullException">Thrown when the reducer is null.</exception>
    public void Map<TAction>(Func<TState, TAction, TState> reducer)
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Reducers[typeof(TAction)] = (state, action) => reducer(state, (TAction)action);
    }
    
    /// <summary>
    /// Maps a reducer function to a specific action type.
    /// </summary>
    /// <param name="reducer">The reducer function that takes only the state and returns a new state.</param>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    public void Map<TAction>(Func<TState, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Reducers[typeof(TAction)] = (state, _) => reducer(state);
    }
    
    /// <summary>
    /// Maps a reducer function to a specific action type.
    /// </summary>
    /// <param name="reducer">The reducer function that takes no arguments and returns a new state.</param>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    public void Map<TAction>(Func<TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Reducers[typeof(TAction)] = (_, _) => reducer();
    }

    /// <summary>
    /// Reduces the state using the appropriate reducer for the given action.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to apply to the state.</param>
    /// <returns>The new state after applying the reducer, or the original state if no reducer is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the action is null.</exception>
    public TState Reduce(TState state, IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Reducers.TryGetValue(action.GetType(), out var reducer)
            ? reducer(state, action)
            : state;
    }

    /// <summary>
    /// Gets the initial state of the reducer.
    /// </summary>
    /// <returns>The initial state.</returns>
    public virtual TState GetInitialState()
    {
        return default!;
    }

    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex LowerCharUpperCharRegex();
}