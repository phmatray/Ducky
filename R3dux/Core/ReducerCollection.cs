using System.Collections;
using R3dux.Temp;

namespace R3dux;

public class ReducerCollection<TState>
{
    protected readonly Dictionary<Type, object> Reducers = new();

    public void Map<TAction>(Func<TState, TAction, TState> reducer)
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(reducer);
        Reducers[typeof(TAction)] = new Reducer<TState, TAction>(reducer);
    }

    public TState Reduce<TAction>(TState state, TAction action)
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Reducers.TryGetValue(action.GetType(), out var reducerObj)
            && reducerObj is Reducer<TState, TAction> reducer)
        {
            return reducer.Reduce(state, action);
        }

        return state;
    }

    public void Remove<TAction>()
        where TAction : IAction
        => Reducers.Remove(typeof(TAction));

    public bool Contains<TAction>()
        where TAction : IAction
        => Reducers.ContainsKey(typeof(TAction));

    public void Clear()
        => Reducers.Clear();

    public IEnumerator GetEnumerator()
        => Reducers.Values.GetEnumerator();
}