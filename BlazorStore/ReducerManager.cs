using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace BlazorStore;

public class ReducerManager<TState> : IDisposable
{
    private readonly ActionsSubject _dispatcher;
    private readonly TState _initialState;
    private IDictionary<string, IActionReducer<TState>> _reducers;
    private readonly IActionReducerFactory<TState> _reducerFactory;
    private readonly BehaviorSubject<IActionReducer<TState>> _reducerSubject;

    public IDictionary<string, IActionReducer<TState>> CurrentReducers => _reducers;
    public IObservable<IActionReducer<TState>> Reducers => _reducerSubject.AsObservable();

    public ReducerManager(
        ActionsSubject dispatcher,
        TState initialState,
        IDictionary<string, IActionReducer<TState>> reducers,
        IActionReducerFactory<TState> reducerFactory)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _initialState = initialState;
        _reducers = reducers ?? throw new ArgumentNullException(nameof(reducers));
        _reducerFactory = reducerFactory ?? throw new ArgumentNullException(nameof(reducerFactory));
        _reducerSubject = new BehaviorSubject<IActionReducer<TState>>(_reducerFactory.CreateReducer(reducers, initialState));
    }

    public void AddReducer(string key, IActionReducer<TState> reducer)
    {
        _reducers[key] = reducer;
        UpdateReducers(new[] { key });
    }

    public void RemoveReducer(string key)
    {
        _reducers = Utils.Omit(_reducers, key);
        UpdateReducers(new[] { key });
    }

    private void UpdateReducers(IEnumerable<string> keys)
    {
        _reducerSubject.OnNext(_reducerFactory.CreateReducer(_reducers, _initialState));
        _dispatcher.OnNext(new UpdateReducerAction());
    }

    public void Dispose()
    {
        _reducerSubject.OnCompleted();
        _reducerSubject.Dispose();
    }
}