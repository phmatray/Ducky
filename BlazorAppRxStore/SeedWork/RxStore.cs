using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace BlazorAppRxStore.SeedWork;

public class RxStore<TState, TReducer>
    where TReducer : ReducerBase<TState>, new()
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
            .Scan(initialState, _reducer.Reduce)
            .Subscribe(_state);
    }

    public void Dispatch(IAction action)
        => _actions.OnNext(action);

    public IObservable<TResult> Select<TResult>(Func<TState, TResult> selector)
        => _state.Select(selector).DistinctUntilChanged();
}