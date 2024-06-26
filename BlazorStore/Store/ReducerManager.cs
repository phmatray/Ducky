namespace BlazorStore;

public class ReducerManager<TState> : IDisposable
{
    private readonly ActionsSubject _dispatcher;
    private readonly TState _initialState;
    private readonly IDictionary<string, IActionReducer<TState>> _reducers;
    private readonly IActionReducerFactory<TState> _reducerFactory;
    private readonly BehaviorSubject<IActionReducer<TState>> _reducerSubject;

    public IDictionary<string, IActionReducer<TState>> CurrentReducers
        => _reducers;
    
    public IObservable<IActionReducer<TState>> Reducers
        => _reducerSubject.AsObservable();

    public ReducerManager(
        ActionsSubject dispatcher,
        TState initialState,
        IDictionary<string, IActionReducer<TState>> reducers,
        IActionReducerFactory<TState> reducerFactory)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(reducers);
        ArgumentNullException.ThrowIfNull(reducerFactory);
        
        _dispatcher = dispatcher;
        _initialState = initialState;
        _reducers = reducers;
        _reducerFactory = reducerFactory;
        _reducerSubject = new BehaviorSubject<IActionReducer<TState>>(_reducerFactory.CreateReducer(reducers, initialState));
    }

    public void AddReducer(string key, IActionReducer<TState> reducer)
    {
        _reducers[key] = reducer;
        UpdateReducers([key]);
    }

    public void RemoveReducer(string key)
    {
        _reducers.Remove(key);
        UpdateReducers([key]);
    }

    private void UpdateReducers(IEnumerable<string> keys)
    {
        _reducerSubject.OnNext(_reducerFactory.CreateReducer(_reducers, _initialState));
        _dispatcher.OnNext(new UpdateReducersAction(keys));
    }

    public void Dispose()
    {
        _reducerSubject.OnCompleted();
        _reducerSubject.Dispose();
    }
}