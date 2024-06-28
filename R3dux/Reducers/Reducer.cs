namespace R3dux;

/// <summary>
/// Abstract class that provides the base implementation for state reducers.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public abstract class Reducer<TState>
    : IReducer<TState>
    where TState : notnull, new()
{
    private readonly Dictionary<Type, ActionReducer<TState, IAction>> _handlers = new();

    /// <summary>
    /// Registers a reducer function for a specific action type.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="reduce">The reducer function.</param>
    protected void Register<TAction>(ActionReducer<TState, TAction> reduce)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (state, action) => reduce(state, (TAction)action);
    
    /// <summary>
    /// Registers a reducer function that does not require an action parameter.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="reduce">The reducer function.</param>
    protected void Register<TAction>(StateReducer<TState> reduce)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (state, _) => reduce(state);
    
    /// <summary>
    /// Registers a reducer function that does not require any parameters.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="reduce">The reducer function.</param>
    protected void Register<TAction>(StateReducerEmpty<TState> reduce)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (_, _) => reduce();

    /// <summary>
    /// Reduces the given state using the specified action.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to be applied.</param>
    /// <returns>The new state after the action is applied.</returns>
    public virtual TState Reduce(TState state, IAction action)
        => _handlers.TryGetValue(action.GetType(), out var handler)
            ? handler(state, action)
            : state;
}