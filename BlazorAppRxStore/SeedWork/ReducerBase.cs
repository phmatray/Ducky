namespace BlazorAppRxStore.SeedWork;

public abstract class ReducerBase<TState>
{
    private readonly Dictionary<Type, Func<TState, IAction, TState>> _handlers = new();

    protected void Register<TAction>(Func<TState, TAction, TState> handler)
        where TAction : IAction
    {
        _handlers[typeof(TAction)] = (state, action) => handler(state, (TAction)action);
    }

    public TState Reduce(TState state, IAction action)
    {
        if (_handlers.TryGetValue(action.GetType(), out var handler))
        {
            return handler(state, action);
        }

        return state;
    }
}