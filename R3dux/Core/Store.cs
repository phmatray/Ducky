using System.Diagnostics;
using R3;

namespace R3dux;

public class Store
    : IStore, IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private readonly Dictionary<string, ISlice> _slices = new();
    private readonly IDispatcher _dispatcher;

    public Store(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        
        State = new ReactiveProperty<RootState>(GetInitialState());
        SubscribeToDispatcherActions();
        IsInitialized = true;
    }

    public bool IsInitialized { get; }
    
    public ReactiveProperty<RootState> State { get; private set; }

    public TState GetState<TState>(string key)
        where TState : notnull, new()
        => State.Value.Select<TState>(key);
    
    public IDispatcher GetDispatcher()
        => _dispatcher;

    public void Dispatch(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcher.Dispatch(action);
    }

    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        _slices.Add(slice.Key, slice);
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

    private void OnDispatch(object action)
    {
        var rootState = _slices
            .Aggregate(State.Value, (state, slice) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var prevState = state[slice.Key];
                // var updatedState = slice.Reducers.Reduce(prevState, action);
                object? updatedState = null;
                state[slice.Key] = updatedState;
                stopwatch.Stop();
                StateLogger.LogStateChange(action, prevState, updatedState, stopwatch.Elapsed.TotalMilliseconds);
                return state;
            }); 

        State.Value = rootState;
    }
    
    private RootState GetInitialState()
    {
        var rootState = new RootState();
        
        foreach (var slice in _slices.Values)
        {
            rootState[slice.Key] = slice.InitialState;
        }
        
        return rootState;
    }
    
    private void SubscribeToDispatcherActions()
    {
        _dispatcher.ActionStream
            .Subscribe(OnDispatch)
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}