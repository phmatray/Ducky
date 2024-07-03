using Microsoft.Extensions.DependencyInjection;
using R3;

namespace BzRx;

public abstract class StateObservable : Observable<object>
{
    public abstract Observable<object> State { get; }
}

public class State<T>
    : ReactiveProperty<T>, IDisposable
{
    private readonly IDisposable _stateSubscription;
    private readonly ScannedActionsSubject _scannedActions;

    public IObservable<T> StateStream { get; }

    public State(
        ActionsSubject actionsSubject,
        Observable<ActionReducer<object, IAction>> reducerObservable,
        ScannedActionsSubject scannedActions,
        T initialState)
        : base(initialState)
    {
        _scannedActions = scannedActions;

        var actionsOnQueue = actionsSubject.ObserveOn(new SynchronizationContext());
        var withLatestReducer = actionsOnQueue.WithLatestFrom(reducerObservable,
            (action, reducer) => new { action, reducer });

        var seed = new StateActionPair<T> { State = initialState };
        var stateAndActionStream = withLatestReducer.Scan(
            seed, (acc, curr) => ReduceState(acc, curr.action, curr.reducer));

        _stateSubscription = stateAndActionStream.Subscribe(pair =>
        {
            OnNext(pair.State);
            _scannedActions.OnNext(pair.Action);
        });

        StateStream = this.AsObservable();
    }

    public void Dispose()
    {
        _stateSubscription.Dispose();
        base.OnCompleted();
    }

    private static StateActionPair<T> ReduceState(StateActionPair<T> stateActionPair, IAction action,
        ActionReducer<object, IAction> reducer)
    {
        var state = stateActionPair.State;
        return new StateActionPair<T>
        {
            State = (T)reducer(state, action),
            Action = action
        };
    }
}

public class StateActionPair<T>
{
    public T State { get; set; }
    public IAction Action { get; set; }
}

public static class StateProvider
{
    public static void AddState<T>(this IServiceCollection services, T initialState)
    {
        services.AddSingleton<State<T>>(provider =>
        {
            var actionsSubject = provider.GetRequiredService<ActionsSubject>();
            var reducerObservable = provider.GetRequiredService<Observable<ActionReducer<object, IAction>>>();
            var scannedActions = provider.GetRequiredService<ScannedActionsSubject>();
            return new State<T>(actionsSubject, reducerObservable, scannedActions, initialState);
        });
        services.AddSingleton<StateObservable>(provider => provider.GetRequiredService<State<T>>());
    }
}
