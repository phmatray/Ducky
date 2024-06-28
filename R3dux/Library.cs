using System.Diagnostics;
using R3;

namespace R3dux;

public interface IAction;


public abstract class Reducer<TState>
{
    protected delegate TState ReduceHandler<in TAction>(TState state, TAction action);
    protected delegate TState ReduceHandler(TState state);
    protected delegate TState ReduceHandlerEmpty();
    
    private readonly Dictionary<Type, ReduceHandler<IAction>> _handlers = new();

    protected void Register<TAction>(ReduceHandler<TAction> reduce)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (state, action) => reduce(state, (TAction)action);
    
    protected void Register<TAction>(ReduceHandler reduce)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (state, _) => reduce(state);
    
    protected void Register<TAction>(ReduceHandlerEmpty reduce)
        where TAction : IAction
        => _handlers[typeof(TAction)] = (_, _) => reduce();

    public virtual TState ReduceAction(TState state, IAction action)
        => _handlers.TryGetValue(action.GetType(), out var handler)
            ? handler(state, action)
            : state;
}


public abstract class Effect<TState>
{
    public abstract Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<TState> state);
}


public class Store<TState>
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

        var newState = _reducer.ReduceAction(prevState, action);
        _stateSubject.Value = newState;

        stopwatch.Stop();
        StateLogger.LogStateChange(action, prevState, newState, stopwatch.Elapsed.TotalMilliseconds);

        _actionSubject.OnNext(action);
    }
}