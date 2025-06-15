// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
    private TState? _currentState;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SliceReducers{TState}"/> class.
    /// </summary>
    protected SliceReducers()
    {
        // Register hydration handler
        RegisterHydrationHandler();
    }


    /// <inheritdoc />
    public virtual event EventHandler? StateUpdated;

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
        Type type = GetType();

        // Use full name for robustness, but clean it up for readability
        string fullTypeName = type.FullName ?? type.Name;

        // Handle generic types by removing generic parameters
        if (type.IsGenericType)
        {
            int genericIndex = fullTypeName.IndexOf('`');
            if (genericIndex > 0)
            {
                fullTypeName = fullTypeName[..genericIndex];
            }
        }

        // Remove common reducer suffixes BEFORE transforming
        if (fullTypeName.EndsWith("Reducers", StringComparison.InvariantCulture))
        {
            fullTypeName = fullTypeName[..^8];
        }
        else if (fullTypeName.EndsWith("Reducer", StringComparison.InvariantCulture))
        {
            fullTypeName = fullTypeName[..^7];
        }

        // Replace namespace dots with dashes for flat key structure
        fullTypeName = fullTypeName.Replace('.', '-');

        // Convert to kebab case
        string kebabCase = LowerCharUpperCharRegex().Replace(fullTypeName, "$1-$2");

        // Return the key in lowercase for consistency
        return kebabCase.ToLower(CultureInfo.InvariantCulture);
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
            _currentState = GetInitialState();
            _isInitialized = true;
        }

        return _currentState is null
            ? throw new DuckyException("State is null.")
            : _currentState;
    }

    /// <inheritdoc />
    public virtual TState CurrentState => _currentState ?? GetInitialState();

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

        // First update the state...
        _currentState = updatedState;

        // ...then notify subscribers that the state has been updated.
        StateUpdated?.Invoke(this, EventArgs.Empty);
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
            StateUpdated = null;
        }

        // Note disposing has been done.
        _disposed = true;
    }

    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex LowerCharUpperCharRegex();

    /// <summary>
    /// Registers the hydration handler to support state persistence.
    /// </summary>
    private void RegisterHydrationHandler()
    {
        Type hydrateActionType = typeof(HydrateSliceAction);
        
        Reducers[hydrateActionType] = (state, action) =>
        {
            if (action is HydrateSliceAction hydrateAction)
            {
                string myKey = GetKey();
                bool keysMatch = hydrateAction.SliceKey == myKey;
                bool isCorrectType = hydrateAction.State is TState;
                
                if (keysMatch && hydrateAction.State is TState hydratedState)
                {
                    return hydratedState;
                }
            }

            return state;
        };
    }
}
