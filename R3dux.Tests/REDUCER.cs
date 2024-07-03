namespace R3dux.Tests;

public interface IReducer<TState, in TAction>
{
    TState Reduce(TState state, TAction action);
}

public class FunctionalReducer<TState, TAction>(
    Func<TState, TAction, TState> reduceFunc)
    : IReducer<TState, TAction>
{
    private readonly Func<TState, TAction, TState> _reduceFunc = 
        reduceFunc
        ?? throw new ArgumentNullException(nameof(reduceFunc));

    public TState Reduce(TState state, TAction action)
        => _reduceFunc(state, action);
}

public class ReducerManager<TState, TAction>
{
    private readonly List<IReducer<TState, TAction>> _reducers = [];

    public void AddReducer(IReducer<TState, TAction> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        _reducers.Add(reducer);
    }

    public TState Reduce(TState state, TAction action)
    {
        return _reducers
            .Aggregate(state, (current, reducer) => reducer.Reduce(current, action));
    }
}


public class REDUCER
{
    
}