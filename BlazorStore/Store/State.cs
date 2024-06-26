using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace BlazorStore;

public class State<TState> : StateObservable<TState>, IDisposable
{
    private readonly BehaviorSubject<TState> _stateSubject;
    private readonly IDisposable _stateSubscription;
    public override TState Value => _stateSubject.Value;

    public State(
        ActionsSubject actionsSubject,
        IObservable<IActionReducer<TState>> reducerObservable,
        TState initialState)
    {
        _stateSubject = new BehaviorSubject<TState>(initialState);

        var actionsOnQueue = actionsSubject.Actions.ObserveOn(Scheduler.Default);
        var withLatestReducer = actionsOnQueue.WithLatestFrom(reducerObservable);

        var seed = new StateActionPair<TState> { State = initialState };
        var stateAndAction = withLatestReducer
            .Scan(seed, (stateActionPair, actionReducer) => ReduceState(stateActionPair, actionReducer));

        _stateSubscription = stateAndAction.Subscribe(pair =>
        {
            _stateSubject.OnNext(pair.State);
            actionsSubject.OnNext(new Action { Type = "SCANNED_ACTION", Payload = pair.Action });
        });
    }

    public override IDisposable Subscribe(IObserver<TState> observer)
    {
        return _stateSubject.Subscribe(observer);
    }

    private StateActionPair<TState> ReduceState(StateActionPair<TState> stateActionPair, (IAction action, IActionReducer<TState> reducer) actionReducerPair)
    {
        var (action, reducer) = actionReducerPair;
        return new StateActionPair<TState> { State = reducer.Invoke(stateActionPair.State, action), Action = action };
    }

    public void Dispose()
    {
        _stateSubscription.Dispose();
        _stateSubject.OnCompleted();
        _stateSubject.Dispose();
    }
}