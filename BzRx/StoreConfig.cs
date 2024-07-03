using Microsoft.Extensions.DependencyInjection;

namespace BzRx;

public class StoreConfig<T, TAction>
    where TAction : IAction
{
    public Func<object> InitialState { get; set; }
    public ActionReducerFactory<T, TAction> ReducerFactory { get; set; }
    public List<MetaReducer<T, TAction>> MetaReducers { get; set; } = new List<MetaReducer<T, TAction>>();
}

public class RootStoreConfig<T, V> 
    : StoreConfig<T, V>
    where V : IAction
{
    public RuntimeChecks RuntimeChecks { get; set; }
}

public class FeatureSlice<T, V>
    where V : IAction
{
    public string Name { get; set; }
    public ActionReducer<T, V> Reducer { get; set; }
}

public static class StoreConfigUtils
{
    public static ActionReducerMap<T, V> CreateStoreReducers<T, V>(object reducers, IServiceProvider serviceProvider)
        where V : IAction
    {
        return reducers is Type injectionToken
            ? (ActionReducerMap<T, V>)serviceProvider.GetRequiredService(injectionToken)
            : (ActionReducerMap<T, V>)reducers;
    }

    public static IEnumerable<StoreFeature<T, V>> CreateFeatureStore<T, V>(IEnumerable<object> configs,
        IEnumerable<StoreFeature<T, V>> featureStores, IServiceProvider serviceProvider) where V : IAction
    {
        var featureStoreList = new List<StoreFeature<T, V>>(featureStores);
        var configList = new List<object>(configs);

        for (int i = 0; i < featureStoreList.Count; i++)
        {
            if (configList[i] is Type injectionToken)
            {
                var config = (StoreConfig<T, V>)serviceProvider.GetRequiredService(injectionToken);
                featureStoreList[i] = new StoreFeature<T, V>
                {
                    Key = featureStoreList[i].Key,
                    ReducerFactory = config.ReducerFactory ?? Utils.CombineReducers,
                    MetaReducers = config.MetaReducers ?? new List<MetaReducer<T, V>>(),
                    InitialState = config.InitialState
                };
            }
        }

        return featureStoreList;
    }

    public static IEnumerable<ActionReducerMap<T, V>> CreateFeatureReducers<T, V>(IEnumerable<object> reducerCollection,
        IServiceProvider serviceProvider) where V : IAction
    {
        var reducers = new List<ActionReducerMap<T, V>>();

        foreach (var reducer in reducerCollection)
        {
            reducers.Add(reducer is Type injectionToken
                ? (ActionReducerMap<T, V>)serviceProvider.GetRequiredService(injectionToken)
                : (ActionReducerMap<T, V>)reducer);
        }

        return reducers;
    }

    public static object InitialStateFactory(object initialState)
    {
        return initialState is Func<object> factory ? factory() : initialState;
    }

    public static IEnumerable<MetaReducer<T, V>> ConcatMetaReducers<T, V>(IEnumerable<MetaReducer<T, V>> metaReducers,
        IEnumerable<MetaReducer<T, V>> userProvidedMetaReducers) where V : IAction
    {
        var result = new List<MetaReducer<T, V>>(metaReducers);
        result.AddRange(userProvidedMetaReducers);
        return result;
    }

    public static object ProvideForRootGuard(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetService<Store<object>>();
        if (store != null)
        {
            throw new InvalidOperationException(
                "The root Store has been provided more than once. Feature modules should provide feature states instead.");
        }

        return "guarded";
    }
}