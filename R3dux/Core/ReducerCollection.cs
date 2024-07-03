using System.Collections;

namespace R3dux;

public class ReducerCollection<TState>
    : IReducer<TState>, IEnumerable
{
    private readonly Dictionary<Type, object> _reducers = [];

    public void Map<TAction>(Func<TState, TAction, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        _reducers[typeof(TAction)] = reducer;
    }

    public TState Reduce(TState state, object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (_reducers.TryGetValue(action.GetType(), out var reducer))
        {
            return ((Func<TState, object, TState>)reducer)(state, action);
        }
        
        return state;
    }

    public void Remove<TAction>()
        => _reducers.Remove(typeof(TAction));

    public bool Contains<TAction>()
        => _reducers.ContainsKey(typeof(TAction));

    public void Clear()
        => _reducers.Clear();

    public IEnumerator GetEnumerator()
        => _reducers.Values.GetEnumerator();
}