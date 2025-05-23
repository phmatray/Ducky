// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using R3;

namespace Ducky;

/// <summary>
/// Represents a strongly-typed state slice with state management and reducers.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this slice.</typeparam>
/// <remarks>
/// Each "slice reducer" is responsible for providing an initial value
/// and calculating the updates to that slice of the state.
/// </remarks>
public abstract partial record SliceReducers<TState>
    : ISlice<TState>, IDisposable
{
    private bool _isInitialized;
    private readonly ReactiveProperty<TState> _state = new();
    private readonly Subject<Unit> _stateUpdated = new();
    private readonly StateLoggerObserver<TState> _stateLoggerObserver = new();
    private bool _disposed;

    /// <inheritdoc />
    public virtual Observable<TState> State => _state;

    /// <inheritdoc />
    public virtual Observable<Unit> StateUpdated => _stateUpdated;

    /// <summary>
    /// Gets the initial state of the reducer.
    /// </summary>
    /// <returns>The initial state.</returns>
    public abstract TState GetInitialState();

    /// <summary>
    /// Gets a dictionary that holds the reducers mapped by the type of action.
    /// </summary>
    public Dictionary<Type, Func<TState, object, TState>> Reducers { get; } = [];

    /// <inheritdoc />
    public virtual string GetKey()
    {
        // get type of the inheriting class
        string typeName = GetType().Name;

        // the key should not end with "Reducers" or "Reducer"
        if (typeName.EndsWith("Reducers", StringComparison.InvariantCulture))
        {
            typeName = typeName[..^8];
        }
        else if (typeName.EndsWith("Reducer", StringComparison.InvariantCulture))
        {
            typeName = typeName[..^7];
        }

        // convert to kebab case
        typeName = LowerCharUpperCharRegex().Replace(typeName, "$1-$2");

        // return the key in lowercase
        return typeName.ToLower(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public virtual Type GetStateType()
    {
        return typeof(TState);
    }

    /// <inheritdoc />
    public virtual object GetState()
    {
        if (!_isInitialized)
        {
            _state.OnNext(GetInitialState());
            _isInitialized = true;
        }

        return _state.Value is null
            ? throw new DuckyException("State is null.")
            : _state.Value;
    }

    /// <summary>
    /// Converts the slice to a JSON object.
    /// </summary>
    /// <returns>The JSON object representing the slice.</returns>
    public virtual JsonObject GetJson()
    {
        JsonObject json = new()
        {
            ["key"] = GetKey(),
            ["state"] = JsonSerializer.SerializeToNode(GetState())
        };

        return json;
    }

    /// <inheritdoc />
    public void OnDispatch(object action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Stopwatch stopwatch = Stopwatch.StartNew();
        var prevState = (TState)GetState();
        TState updatedState = Reduce(prevState, action);
        stopwatch.Stop();

        if (prevState?.Equals(updatedState) == true)
        {
            return;
        }

        StateChange<TState> stateChange = new(
            action,
            prevState,
            updatedState,
            stopwatch.Elapsed.TotalMilliseconds);

        _stateLoggerObserver.OnNext(stateChange);

        // First update the state...
        _state.OnNext(updatedState);

        // ...then notify subscribers that the state has been updated.
        _stateUpdated.OnNext(Unit.Default);
    }

    /// <summary>
    /// Maps a reducer function to a specific action type.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="reducer">The reducer function that takes the state and action and returns a new state.</param>
    /// <exception cref="ArgumentNullException">Thrown when the reducer is null.</exception>
    public void On<TAction>(Func<TState, TAction, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Reducers[typeof(TAction)] = (state, action) => reducer(state, (TAction)action);
    }

    /// <summary>
    /// Maps a reducer function to a specific action type.
    /// </summary>
    /// <param name="reducer">The reducer function that takes only the state and returns a new state.</param>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    public void On<TAction>(Func<TState, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Reducers[typeof(TAction)] = (state, _) => reducer(state);
    }

    /// <summary>
    /// Maps a reducer function to a specific action type.
    /// </summary>
    /// <param name="reducer">The reducer function that takes no arguments and returns a new state.</param>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    public void On<TAction>(Func<TState> reducer)
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
    public TState Reduce(TState state, object action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Reducers.TryGetValue(action.GetType(), out Func<TState, object, TState>? reducer)
            ? reducer(state, action)
            : state;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="SliceReducers{TState}"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">If true, the method has been called directly or indirectly by a user's code. Managed and unmanaged resources can be disposed. If false, the method has been called by the runtime from inside the finalizer and only unmanaged resources can be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources.
            _state.Dispose();
            _stateUpdated.Dispose();
            _stateLoggerObserver.Dispose();
        }

        // Note disposing has been done.
        _disposed = true;
    }

    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex LowerCharUpperCharRegex();
}
