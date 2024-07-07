using System.Collections;
using R3dux.Temp;

namespace R3dux;

/// <summary>
/// Represents a collection of reducers for a specific state type.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public class ReducerCollection<TState>
{
    /// <summary>
    /// A dictionary that holds the reducers mapped by the type of action.
    /// </summary>
    protected readonly Dictionary<Type, object> Reducers = new();

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
        Reducers[typeof(TAction)] = new Reducer<TState, TAction>(reducer);
    }

    /// <summary>
    /// Reduces the state using the appropriate reducer for the given action.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to apply to the state.</param>
    /// <returns>The new state after applying the reducer, or the original state if no reducer is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the action is null.</exception>
    public TState Reduce<TAction>(TState state, TAction action)
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Reducers.TryGetValue(action.GetType(), out var reducerObj)
            && reducerObj is Reducer<TState, TAction> reducer)
        {
            return reducer.Reduce(state, action);
        }

        return state;
    }

    /// <summary>
    /// Removes the reducer mapped to the specified action type.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    public void Remove<TAction>()
        where TAction : IAction
        => Reducers.Remove(typeof(TAction));

    /// <summary>
    /// Checks if a reducer for the specified action type exists in the collection.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <returns><c>true</c> if a reducer for the specified action type exists; otherwise, <c>false</c>.</returns>
    public bool Contains<TAction>()
        where TAction : IAction
        => Reducers.ContainsKey(typeof(TAction));

    /// <summary>
    /// Clears all reducers from the collection.
    /// </summary>
    public void Clear()
        => Reducers.Clear();

    /// <summary>
    /// Returns an enumerator that iterates through the collection of reducers.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator GetEnumerator()
        => Reducers.Values.GetEnumerator();
}
