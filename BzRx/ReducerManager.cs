using Microsoft.Extensions.DependencyInjection;
using R3;

namespace BzRx;

public abstract class ReducerObservable
    : Observable<ActionReducer<object, IAction>>;

public abstract class ReducerManagerDispatcher
    : ActionsSubject;

public class UpdateAction : RxAction
{
    public override string Type { get; set; } = "@bzrx/store/update-reducers";
    public List<string> Features { get; set; }
}

public class ReducerManager
    : ReactiveProperty<ActionReducer<object, IAction>>, IDisposable
{
    private ActionReducerMap<object, IAction> _reducers;
    private readonly ReducerManagerDispatcher _dispatcher;
    private readonly object _initialState;
    private readonly ActionReducerFactory<object, IAction> _reducerFactory;

    public ActionReducerMap<object, IAction> CurrentReducers => _reducers;

    public ReducerManager(
        ReducerManagerDispatcher dispatcher,
        object initialState,
        ActionReducerMap<object, IAction> reducers,
        ActionReducerFactory<object, IAction> reducerFactory
    ) : base(reducerFactory(reducers, initialState))
    {
        _dispatcher = dispatcher;
        _initialState = initialState;
        _reducers = reducers;
        _reducerFactory = reducerFactory;
    }

    public void AddFeature(StoreFeature<object, IAction> feature)
    {
        AddFeatures([feature]);
    }

    public void AddFeatures(List<StoreFeature<object, IAction>> features)
    {
        var reducers = new Dictionary<string, ActionReducer<object, IAction>>();

        foreach (var feature in features)
        {
            var reducer = feature.Reducers is ActionReducer<object, IAction> singleReducer
                ? Utils.CreateFeatureReducerFactory(feature.MetaReducers)(singleReducer, feature.InitialState)
                : Utils.CreateReducerFactory(feature.ReducerFactory, feature.MetaReducers)(feature.Reducers, feature.InitialState);

            reducers[feature.Key] = reducer;
        }

        AddReducers(reducers);
    }

    public void RemoveFeature(StoreFeature<object, IAction> feature)
    {
        RemoveFeatures(new List<StoreFeature<object, IAction>> { feature });
    }

    public void RemoveFeatures(List<StoreFeature<object, IAction>> features)
    {
        RemoveReducers(features.ConvertAll(f => f.Key));
    }

    public void AddReducer(string key, ActionReducer<object, IAction> reducer)
    {
        AddReducers(new Dictionary<string, ActionReducer<object, IAction>> { { key, reducer } });
    }

    public void AddReducers(Dictionary<string, ActionReducer<object, IAction>> reducers)
    {
        foreach (var kvp in reducers)
        {
            _reducers[kvp.Key] = kvp.Value;
        }
        UpdateReducers(reducers.Keys);
    }

    public void RemoveReducer(string featureKey)
    {
        RemoveReducers(new List<string> { featureKey });
    }

    public void RemoveReducers(List<string> featureKeys)
    {
        foreach (var key in featureKeys)
        {
            _reducers.Remove(key);
        }
        UpdateReducers(featureKeys);
    }

    private void UpdateReducers(IEnumerable<string> featureKeys)
    {
        OnNext(_reducerFactory(_reducers, _initialState));
        _dispatcher.OnNext(new UpdateAction
        {
            Type = UpdateAction.Type,
            Features = new List<string>(featureKeys)
        });
    }

    public new void Dispose()
    {
        base.OnCompleted();
    }
}

public static class ReducerManagerProvider
{
    public static void AddReducerManager(this IServiceCollection services)
    {
        services.AddSingleton<ReducerManager>();
        services.AddSingleton<ReducerObservable>(sp => sp.GetRequiredService<ReducerManager>());
        services.AddSingleton<ReducerManagerDispatcher>(sp => sp.GetRequiredService<ActionsSubject>());
    }
}