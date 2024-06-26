using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace BlazorStore;

public class RxStore<TState, TReducer>
    where TReducer : IActionReducer<TState>, new()
{
    private readonly BehaviorSubject<TState> _state;
    private readonly ActionsSubject _actionsObserver;
    private readonly TReducer _reducer;
    
    private readonly ReducerManager<TState> _reducerManager; 

    public IObservable<TState> State => _state.AsObservable();
    public IObservable<IAction> Actions => _actionsObserver.Actions;
    
    public RxStore(ActionsSubject actionsSubject)
        : this(actionsSubject, Activator.CreateInstance<TState>())
    {
    }
    
    public RxStore(ActionsSubject actionsSubject, TState initialState)
    {
        _state = new BehaviorSubject<TState>(initialState);
        _actionsObserver = actionsSubject ?? throw new ArgumentNullException(nameof(actionsSubject));
        _reducer = new TReducer();

        _actionsObserver.Actions
            .Scan(initialState, Reduce)
            .Subscribe(_state);
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

    private TState Reduce(TState state, IAction action)
    {
        var stopwatch = Stopwatch.StartNew();
        var newState = _reducer.Invoke(state, action);
        stopwatch.Stop();
        StateLogger.LogStateChange(action, state, newState, stopwatch.Elapsed.TotalMilliseconds);
        return newState;
    }
}