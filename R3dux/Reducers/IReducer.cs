namespace R3dux;

/// <summary>
/// Interface for reducing a state with an action.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public interface IReducer<TState>
    where TState : notnull, new()
{
    /// <summary>
    /// Reduces the given state using the specified action.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to be applied.</param>
    /// <returns>The new state after the action is applied.</returns>
    TState Reduce(TState state, IAction action);
}