using R3;

namespace R3dux;

public class Store
    : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly CompositeDisposable _disposables;
    private readonly ObservableSlices _slices;
    private readonly object _lock;
    private bool _isDisposed;

    public Store(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        
        _dispatcher = dispatcher;
        _disposables = new CompositeDisposable();
        _slices = new ObservableSlices();
        _lock = new object();
        
        DispatchStoreInitialized();
    }
    
    public IDispatcher Dispatcher
        => _dispatcher;

    public Observable<RootState> RootStateObservable
    {
        get
        {
            lock (_lock)
            {
                return _slices.RootStateObservable;
            }
        }
    }

    public TState GetState<TState>(string key)
        where TState : notnull, new()
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_slices)
        {
            return _slices.RootState.GetSliceState<TState>(key);
        }
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

    public void AddSlices(params ISlice[] slices)
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

        lock (_lock)
        {
            // Add the slice to the ObservableSlices collection
            _slices.AddSlice(slice);
        }

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
        lock (_lock)
        {
            _slices.ReplaceSlice(slice.GetKey(), slice);
        }
    }

    public void AddEffects(params IEffect[] effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        
        foreach (var effect in effects)
        {
            AddEffect(effect);
        }
    }
    
    public void AddEffect(IEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        
        effect
            .Handle(_dispatcher.ActionStream, RootStateObservable)
            .Subscribe(Dispatch)
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