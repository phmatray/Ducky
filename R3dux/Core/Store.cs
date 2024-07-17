using Microsoft.Extensions.Logging;
using R3;

namespace R3dux;

public sealed class Store
    : IStore, IDisposable
{
    private readonly ILogger<Store> _logger;
    private readonly CompositeDisposable _disposables = [];
    private readonly ObservableSlices _slices = new();
    private bool _isDisposed;

    public Store(IDispatcher dispatcher, ILogger<Store> logger)
    {
        _logger = logger;
        ArgumentNullException.ThrowIfNull(dispatcher);
        Dispatcher = dispatcher;
        Dispatcher.Dispatch(new StoreInitialized());
    }
    
    public IDispatcher Dispatcher { get; }

    public Observable<RootState> RootStateObservable
        => _slices.RootStateObservable;
    
    public TState GetState<TState>(string key)
        where TState : notnull, new()
        => _slices.RootState.GetSliceState<TState>(key);

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

        // Add the slice to the ObservableSlices collection
        _slices.AddSlice(slice);

        // Subscribe the slice to the dispatcher's action stream
        Dispatcher.ActionStream
            .Subscribe(slice.OnDispatch)
            .AddTo(_disposables);

        // Update the root state when a slice state is updated
        slice.StateUpdated
            .Subscribe(_ => _slices.ReplaceSlice(slice.GetKey(), slice))
            .AddTo(_disposables);
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
            .Handle(Dispatcher.ActionStream, RootStateObservable)
            .Subscribe(Dispatcher.Dispatch)
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