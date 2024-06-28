using System.Diagnostics;
using R3;

namespace R3dux;

public interface IAction;

public abstract class Effect<TState>
{
    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<TState> state);
}


public class Store<TState>
    where TState : notnull, new()
{
    private readonly Reducer<TState> _reducer;
    private readonly ReactiveProperty<TState> _stateSubject;
    private readonly Subject<IAction> _actionSubject;

    public Store(
        TState initialState,
        Reducer<TState> reducer,
        IEnumerable<Effect<TState>> effects)
    {
        _reducer = reducer;
        _stateSubject = new ReactiveProperty<TState>(initialState);
        _actionSubject = new Subject<IAction>();

        foreach (var effect in effects)
        {
            effect
                .Handle(_actionSubject, _stateSubject)
                .Subscribe(Dispatch);
        }
    }

    public Observable<TState> State => _stateSubject;

    public void Dispatch(IAction action)
    {
        var prevState = _stateSubject.Value;
        var stopwatch = Stopwatch.StartNew();

        var newState = _reducer.Reduce(prevState, action);
        _stateSubject.Value = newState;

        stopwatch.Stop();
        StateLogger.LogStateChange(action, prevState, newState, stopwatch.Elapsed.TotalMilliseconds);

        _actionSubject.OnNext(action);
    }
}