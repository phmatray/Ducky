using System.Collections.ObjectModel;
using System.Diagnostics;
using R3;
using R3dux.Temp;

namespace R3dux;

public class Store
    : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly CompositeDisposable _disposables;
    private readonly ObservableCollection<ISlice> _slices;

    public Store(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        
        _dispatcher = dispatcher;
        _disposables = [];
        _slices = [];
        
        SubscribeToDispatcherActions();
        State = new ReactiveProperty<RootState>(new RootState());
        
        // TODO: Dispatch an initial action to set the initial state of the store
        IsInitialized = true;
    }

    public bool IsInitialized { get; }
    
    public ReactiveProperty<RootState> State { get; }

    public RootState GetRootState()
        => State.Value;

    public TState GetState<TState>(string key)
        where TState : notnull, new()
        => State.Value.Select<TState>(key);
    
    public void Dispatch(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcher.Dispatch(action);
    }

    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        _slices.Add(slice);
        UpdateState(slice);
    }

    public void AddSlices(IEnumerable<ISlice> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        foreach (var slice in slices)
        {
            AddSlice(slice);
        }
    }
    
    public void AddEffect(IEffect effect)
    {
        effect
            .Handle(_dispatcher.ActionStream, this)
            .Subscribe(_dispatcher.Dispatch)
            .AddTo(_disposables);
    }
    
    public void AddEffects(IEnumerable<IEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        foreach (var effect in effects)
        {
            AddEffect(effect);
        }
    }

    private void OnDispatch(IAction action)
    {
        var rootState = _slices
            .Aggregate(State.Value, (RootState currentRootState, ISlice slice) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var prevState = currentRootState[slice.Key];
                var updatedState = slice.Reduce(prevState, action);
                currentRootState[slice.Key] = updatedState;
                stopwatch.Stop();
                StateLogger.LogStateChange(action, prevState, updatedState, stopwatch.Elapsed.TotalMilliseconds);
                return currentRootState;
            }); 

        State.OnNext(rootState);
    }
    
    private void SubscribeToDispatcherActions()
    {
        _dispatcher.ActionStream
            .Subscribe(OnDispatch)
            .AddTo(_disposables);
    }

    private void UpdateState(ISlice slice)
    {
        var currentState = State.Value;
        currentState[slice.Key] = slice.InitialState;
        State.Value = currentState;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}