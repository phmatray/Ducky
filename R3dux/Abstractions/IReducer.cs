using R3dux.Temp;

namespace R3dux;

/// <summary>
/// Represents a reducer that processes actions and returns a new state.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <typeparam name="TAction">The type of the action.</typeparam>
public interface IReducer<TState, in TAction>
    where TAction : IAction
{
    /// <summary>
    /// Reduces the specified state using the provided action.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to be processed.</param>
    /// <returns>The new state after the action is applied.</returns>
    TState Reduce(TState state, TAction action);
}
