using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace BlazorAppRxStore.SeedWork;

public class RxStore<TState>
{
    private readonly BehaviorSubject<TState> _state;
    private readonly Subject<IAction> _actions;

    public IObservable<TState> State
        => _state.AsObservable();

    public RxStore(TState initialState, Func<TState, IAction, TState> reducer)
    {
        _state = new BehaviorSubject<TState>(initialState);
        _actions = new Subject<IAction>();

        _actions
            .Scan(initialState, reducer)
            .Subscribe(_state);
    }

    public void Dispatch(IAction action)
    {
        _actions.OnNext(action);
    }

    public IObservable<TResult> Select<TResult>(
        Func<TState, TResult> selector)
    {
        return _state
            .Select(selector)
            .DistinctUntilChanged();
    }
}