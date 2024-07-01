using R3;

namespace R3dux;

public class ReducerCollection<TState>
    where TState : notnull, new()
{
    private readonly Subject<ActionReducer<TState, IAction>> _addReducerSubject = new();
    private readonly Subject<ActionReducer<TState, IAction>> _removeReducerSubject = new();
    private readonly List<ActionReducer<TState, IAction>> _reducers = [];

    public Observable<ActionReducer<TState, IAction>> ReducersAdded
        => _addReducerSubject.AsObservable();
    
    public Observable<ActionReducer<TState, IAction>> ReducersRemoved
        => _removeReducerSubject.AsObservable();

    public void AddReducer(ActionReducer<TState, IAction> reducer)
    {
        _reducers.Add(reducer);
        _addReducerSubject.OnNext(reducer);
    }

    public void AddReducers(IEnumerable<ActionReducer<TState, IAction>> reducers)
    {
        foreach (var reducer in reducers)
        {
            AddReducer(reducer);
        }
    }

    public void RemoveReducer(ActionReducer<TState, IAction> reducer)
    {
        if (_reducers.Remove(reducer))
        {
            _removeReducerSubject.OnNext(reducer);
        }
    }

    public void RemoveReducers(IEnumerable<ActionReducer<TState, IAction>> reducers)
    {
        foreach (var reducer in reducers)
        {
            RemoveReducer(reducer);
        }
    }
}
