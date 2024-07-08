using System.Diagnostics;
using R3;
using R3dux.Exceptions;

namespace R3dux;

/// <summary>
/// Represents a strongly-typed state slice with state management and reducers.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this slice.</typeparam>
public abstract record Slice<TState> : ISlice<TState>
{
    private bool _isInitialized;
    private readonly ReactiveProperty<TState> _state = new();
    private readonly Subject<Unit> _stateUpdated = new();

    /// <inheritdoc />
    public virtual Observable<TState> State => _state;
    
    /// <inheritdoc />
    public virtual Observable<Unit> StateUpdated => _stateUpdated;

    /// <inheritdoc />
    public abstract ReducerCollection<TState> Reducers { get; }
    
    /// <inheritdoc />
    public abstract string GetKey();

    /// <inheritdoc />
    public abstract TState GetInitialState();

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
        var updatedState = Reducers.Reduce(prevState, action);
        stopwatch.Stop();
        
        if (prevState?.Equals(updatedState) == true)
        {
            return;
        }
        
        _state.OnNext(updatedState);
        _stateUpdated.OnNext(Unit.Default);
        StateLogger.LogStateChange(action, prevState, updatedState, stopwatch.Elapsed.TotalMilliseconds);
    }
}