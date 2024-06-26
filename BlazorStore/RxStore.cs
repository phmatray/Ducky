using System.Reactive.Linq;

namespace BlazorStore;

public class RxStore<TState>
{
    private readonly State<TState> _state;
    private readonly ActionsSubject _actionsObserver;
    private readonly ReducerManager<TState> _reducerManager;

    public IObservable<TState> State => _state.AsObservable();
    public IObservable<IAction> Actions => _actionsObserver.Actions;
    
    public RxStore(
        State<TState> state,
        ActionsSubject actionsObserver,
        ReducerManager<TState> reducerManager)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actionsObserver);
        ArgumentNullException.ThrowIfNull(reducerManager);
        
        _state = state;
        _actionsObserver = actionsObserver;
        _reducerManager = reducerManager;
    }

    public IObservable<TResult> Select<TResult>(Func<TState, TResult> selector)
        => _state.Select(selector).DistinctUntilChanged();

    public void Dispatch(IAction action)
        => _actionsObserver.OnNext(action);
    
    [Obsolete("Use Dispatch instead.")]
    public void Next(IAction action)
        => _actionsObserver.OnNext(action);

    public void Error(Exception error)
        => _actionsObserver.OnError(error);

    public void Complete()
        => _actionsObserver.OnCompleted();
    
    public void AddReducer(string key, IActionReducer<TState> reducer)
        => _reducerManager.AddReducer(key, reducer);

    public void RemoveReducer(string key)
        => _reducerManager.RemoveReducer(key);
}