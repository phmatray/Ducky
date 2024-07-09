using ObservableCollections;
using R3;

namespace R3dux;

public class Store
    : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly CompositeDisposable _disposables;
    private readonly ReactiveProperty<RootState> _rootState;
    private readonly IObservableCollection<ISlice> _slices;
    private bool _isDisposed;

    public Store(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        
        _dispatcher = dispatcher;
        _disposables = [];
        _rootState = new ReactiveProperty<RootState>(new RootState());
        _slices = new ObservableList<ISlice>();
        
        // refresh the root state when a slice state is updated
        _slices.ObserveAdd()
            .Subscribe(ev => UpdateRootState(ev.Value))
            .AddTo(_disposables);
        
        DispatchStoreInitialized();
    }
    
    public IDispatcher Dispatcher
        => _dispatcher;

    public Observable<RootState> RootState
        => _rootState;

    public TState GetState<TState>(string key)
        where TState : notnull, new()
    {
        ArgumentNullException.ThrowIfNull(key);
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
        _rootState.OnNext(_rootState.Value);
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
        ArgumentNullException.ThrowIfNull(effect);
        
        effect
            .Handle(_dispatcher.ActionStream, RootState)
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