using System.Diagnostics;
using R3;
using R3dux.Temp;

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
        
        State = new ReactiveProperty<RootState>(new RootState());
        SubscribeToDispatcherActions();
        IsInitialized = true;
    }

    public bool IsInitialized { get; }
    
    public ReactiveProperty<RootState> State { get; private set; }

    public RootState GetRootState()
        => State.Value;

    public TState GetState<TState>(string key)
        where TState : notnull, new()
        => State.Value.Select<TState>(key);
    
    // TODO: GetStates method

    public void Dispatch(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcher.Dispatch(action);
    }

    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        _slices.Add(slice.Key, slice);
        State.Value[slice.Key] = slice.InitialState;
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
        var rootState = _slices.Values
            .Aggregate(State.Value, (RootState rootState, ISlice slice) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var prevState = rootState[slice.Key];
                var updatedState = slice.Reduce(prevState, action);
                rootState[slice.Key] = updatedState;
                stopwatch.Stop();
                StateLogger.LogStateChange(action, prevState, updatedState, stopwatch.Elapsed.TotalMilliseconds);
                return rootState;
            }); 

        State.OnNext(rootState);
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