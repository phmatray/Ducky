namespace R3dux;

/// <summary>
/// Delegate for reducing a state with an action.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <typeparam name="TAction">The type of the action.</typeparam>
/// <param name="state">The current state.</param>
/// <param name="action">The action to be applied.</param>
/// <returns>The new state after the action is applied.</returns>
public delegate TState ActionReducer<TState, in TAction>(TState state, TAction action)
    where TState : notnull, new()
    where TAction : IAction;

/// <summary>
/// Delegate for reducing a state without any action parameter.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <param name="state">The current state.</param>
/// <returns>The new state.</returns>
public delegate TState StateReducer<TState>(TState state)
    where TState : notnull, new();

/// <summary>
/// Delegate for reducing a state with no input parameters.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <returns>The new state.</returns>
public delegate TState StateReducerEmpty<out TState>()
    where TState : notnull, new();
