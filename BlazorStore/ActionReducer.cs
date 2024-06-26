namespace BlazorStore;

public abstract class ActionReducer<TState> : IActionReducer<TState>
{
    private readonly Dictionary<Type, Func<TState, IAction, TState>> _handlers = new();

    protected void Register<TAction>(Func<TState, TAction, TState> handler)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (state, action) => handler(state, (TAction)action);

    protected void Register<TAction>(Func<TState, TState> handler)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (state, _) => handler(state);

    protected void Register<TAction>(Func<TState> handler)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (_, _) => handler();

    public TState Invoke(TState state, IAction action)
        => _handlers.TryGetValue(action.GetType(), out var handler)
            ? handler(state, action)
            : state;
}