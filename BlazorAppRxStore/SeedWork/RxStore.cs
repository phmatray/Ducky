using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace BlazorAppRxStore.SeedWork;

public class RxStore<TState, TReducer>
    where TReducer : IReducer<TState>, new()
{
    private readonly BehaviorSubject<TState> _state;
    private readonly Subject<IAction> _actions;
    private readonly TReducer _reducer;

    public IObservable<TState> State
        => _state.AsObservable();
    
    public RxStore()
        : this(Activator.CreateInstance<TState>())
    {
    }

    private RxStore(TState initialState)
    {
        _state = new BehaviorSubject<TState>(initialState);
        _actions = new Subject<IAction>();
        _reducer = new TReducer();

        _actions
            .Scan(initialState, Reduce)
            .Subscribe(_state);
    }

    public IObservable<TResult> Select<TResult>(Func<TState, TResult> selector)
        => _state.Select(selector).DistinctUntilChanged();

    public void Dispatch(IAction action)
        => _actions.OnNext(action);

    private TState Reduce(TState state, IAction action)
    {
        var stopwatch = Stopwatch.StartNew();
        var newState = _reducer.Reduce(state, action);
        stopwatch.Stop();
        StateLogger.LogStateChange(action, state, newState, stopwatch.Elapsed.TotalMilliseconds);
        return newState;
    }
}