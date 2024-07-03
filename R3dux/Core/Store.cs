using System.Diagnostics;
using R3;

namespace R3dux;

public class Store
    : IStore, IDisposable
{
    private readonly ReactiveProperty<RootState> _stateSubject;
    private readonly CompositeDisposable _disposables = [];
    private readonly SliceCollection _slices;
    private readonly IDispatcher _dispatcher;

    public Store(
        SliceCollection slices,
        IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _slices = slices;
        _dispatcher = dispatcher;
        _stateSubject = new(slices.GetInitialState());
        _stateSubject.AddTo(_disposables);
        
        SubscribeToDispatcherActions();
        SubscribeToEffects();
        
        IsInitialized = true;
    }

    public bool IsInitialized { get; }
    
    public Observable<RootState> GetStateStream()
        => _stateSubject;

    public RootState GetState()
        => _stateSubject.Value;
    
    public TState GetState<TState>(string key)
        where TState : notnull, new()
        => GetState().Select<TState>(key);
    
    public IDispatcher GetDispatcher()
        => _dispatcher;

    public void Dispatch(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcher.Dispatch(action);
    }

    private void OnDispatch(object action)
    {
        var rootState = _slices
            .Aggregate(_stateSubject.Value, (state, slice) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var prevState = state[slice.Key];
                var updatedState = slice.Reducers.Reduce(prevState, action);
                state[slice.Key] = updatedState;
                stopwatch.Stop();
                StateLogger.LogStateChange(action, prevState, updatedState, stopwatch.Elapsed.TotalMilliseconds);
                return state;
            }); 

        _stateSubject.Value = rootState;
    }
    
    private void SubscribeToDispatcherActions()
    {
        _dispatcher.ActionStream
            .Subscribe(OnDispatch)
            .AddTo(_disposables);
    }
    
    private void SubscribeToEffects()
    {
        foreach (var effect in _slices.GetEffects())
        {
            SubscribeToEffect(effect);
        }
    }

    private void SubscribeToEffect(IEffect effect)
    {
        effect
            .Handle(_dispatcher.ActionStream, this)
            .Subscribe(_dispatcher.Dispatch)
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}