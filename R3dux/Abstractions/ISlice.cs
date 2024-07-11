using R3;

namespace R3dux;

/// <summary>
/// Represents a state slice with basic state management capabilities.
/// </summary>
public interface ISlice
{
    /// <summary>
    /// Gets an observable sequence that produces a notification when the state is updated.
    /// </summary>
    /// <value>The observable sequence of state updates.</value>
    Observable<Unit> StateUpdated { get; }
    
    /// <summary>
    /// Gets the unique key for this state slice.
    /// </summary>
    /// <returns>The unique key as a string.</returns>
    string GetKey();
    
    /// <summary>
    /// Gets the type of the state managed by this slice.
    /// </summary>
    /// <returns>The type of the state.</returns>
    Type GetStateType();

    /// <summary>
    /// Gets the current state of this slice.
    /// </summary>
    /// <returns>The current state as an object.</returns>
    object GetState();

    /// <summary>
    /// Handles the dispatch of an action.
    /// </summary>
    /// <param name="action">The action to be dispatched.</param>
    void OnDispatch(IAction action);
}

/// <summary>
/// Represents a strongly-typed state slice with state management and reducers.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this slice.</typeparam>
public interface ISlice<TState> : ISlice
{
    /// <summary>
    /// Gets an observable sequence that produces the state of this slice.
    /// </summary>
    /// <value>The observable sequence of the state.</value>
    Observable<TState> State { get; }
    
    /// <summary>
    /// Gets the collection of reducers for this state slice.
    /// </summary>
    /// <value>The collection of reducers.</value>
    ReducerCollection<TState> Reducers { get; }
}