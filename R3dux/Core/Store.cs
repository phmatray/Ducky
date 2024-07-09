using R3;

namespace R3dux;

public class Store
    : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly CompositeDisposable _disposables;
    private readonly ReactiveProperty<RootState> _rootState;
    private bool _isDisposed;

    public Store(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        
        _dispatcher = dispatcher;
        _disposables = [];
        _rootState = new ReactiveProperty<RootState>(new RootState());
        
        DispatchStoreInitialized();
    }
    
    public IDispatcher Dispatcher
        => _dispatcher;

    public Observable<RootState> RootState
        => _rootState;

    // public RootState GetRootState()
    // {
    //     return RootState.Value;
    // }

    public TState GetState<TState>(string key)
        where TState : notnull, new()
    {
        return _rootState.Value.GetSliceState<TState>(key);
    }

    public void Dispatch(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(Store));
        }
        
        _dispatcher.Dispatch(action);
    }
    
    private void DispatchStoreInitialized()
    {
        Dispatch(new StoreInitialized());
    }

    public void AddSlices(IEnumerable<ISlice> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        foreach (var slice in slices)
        {
            AddSlice(slice);
        }
    }

    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        
        // Initialize the slice state in the root state
        UpdateRootState(slice);
        
        // Subscribe the slice to the dispatcher's action stream
        _dispatcher.ActionStream
            .Subscribe(slice.OnDispatch)
            .AddTo(_disposables);
        
        // Update the root state when a slice state is updated
        slice.StateUpdated
            .Subscribe(_ => UpdateRootState(slice))
            .AddTo(_disposables);
    }

    private void UpdateRootState(ISlice slice)
    {
        _rootState.Value.AddOrUpdateSliceState(slice.GetKey(), slice.GetState());
    }

    public void AddEffects(IEnumerable<IEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        foreach (var effect in effects)
        {
            AddEffect(effect);
        }
    }
    
    public void AddEffect(IEffect effect)
    {
        effect
            .Handle(_dispatcher.ActionStream, RootState)
            .Subscribe(_dispatcher.Dispatch)
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _disposables.Dispose();
        }
    }
}