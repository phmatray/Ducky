namespace BlazorStore;

/// <summary>
/// Defines a method that a class implements to handle state transitions based on actions.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public interface IReducer<TState>
{
    /// <summary>
    /// Reduces the state based on the given action.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to handle.</param>
    /// <returns>The new state after the action has been applied.</returns>
    TState Reduce(TState state, IAction action);
}