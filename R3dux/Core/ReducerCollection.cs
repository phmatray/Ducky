using System.Text.RegularExpressions;

namespace R3dux;

/// <summary>
/// Represents a collection of reducers for a specific state type.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <remarks>
/// Each "slice reducer" is responsible for providing an initial value
/// and calculating the updates to that slice of the state.
/// </remarks>
public abstract partial class ReducerCollection<TState>
{
    /// <summary>
    /// A dictionary that holds the reducers mapped by the type of action.
    /// </summary>
    public Dictionary<Type, Func<TState, IAction, TState>> Reducers { get; } = new();

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

    /// <summary>
    /// Gets the unique key for this reducers slice.
    /// </summary>
    /// <returns>The unique key as a string.</returns>
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

    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex LowerCharUpperCharRegex();
}
