namespace R3dux;

/// <summary>
/// Represents a pair of state and action.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <typeparam name="TAction">The type of the action, which must implement <see cref="IAction"/>.</typeparam>
/// <param name="State">The current state.</param>
/// <param name="Action">The action to be performed.</param>
public record StateActionPair<TState, TAction>(TState State, TAction Action)
    where TAction : IAction;