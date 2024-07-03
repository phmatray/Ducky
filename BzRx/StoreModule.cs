using Microsoft.Extensions.DependencyInjection;

namespace BzRx;

public class StoreRootModule
{
    public StoreRootModule(
        ActionsSubject actionsSubject,
        ReducerObservable reducerObservable,
        ScannedActionsSubject scannedActionsSubject,
        Store<object> store,
        object? guard = null,
        object? actionCheck = null)
    {
        // Constructor logic
    }
}

public class StoreFeatureModule : IDisposable
{
    private readonly List<StoreFeature<object, IAction>> _features;
    private readonly ActionReducerMap<object, IAction>[] _featureReducers;
    private readonly ReducerManager _reducerManager;

    public StoreFeatureModule(
        List<StoreFeature<object, IAction>> features,
        ActionReducerMap<object, IAction>[] featureReducers,
        ReducerManager reducerManager,
        StoreRootModule root,
        object? actionCheck = null)
    {
        _features = features;
        _featureReducers = featureReducers;
        _reducerManager = reducerManager;

        var feats = new List<StoreFeature<object, IAction>>();

        for (int index = 0; index < features.Count; index++)
        {
            var feature = features[index];
            var featureReducerCollection = featureReducers.Length > 0 ? featureReducers[0] : null;
            if (featureReducerCollection == null)
            {
                continue;
            }

            var reducers = featureReducerCollection;

            feats.Add(new StoreFeature<object, IAction>
            {
                Key = feature.Key,
                Reducers = reducers,
                InitialState = StoreConfigUtils.InitialStateFactory(feature.InitialState),
                ReducerFactory = feature.ReducerFactory,
                MetaReducers = feature.MetaReducers
            });

            _featureReducers = _featureReducers.Skip(1).ToArray(); // Remove the first element
        }

        _reducerManager.AddFeatures(feats.ToList());
    }

    public void Dispose()
    {
        _reducerManager.RemoveFeatures(_features);
    }
}

public static class StoreModule
{
    public static IServiceCollection AddStoreRoot<T, V>(this IServiceCollection services,
        ActionReducerMap<T, V> reducers = null,
        RootStoreConfig<T, V> config = null)
        where V : IAction
    {
        services.AddSingleton(provider =>
        {
            var rootModule = new StoreRootModule(
                provider.GetRequiredService<ActionsSubject>(),
                provider.GetRequiredService<ReducerObservable>(),
                provider.GetRequiredService<ScannedActionsSubject>(),
                provider.GetRequiredService<Store<object>>(),
                provider.GetService<object>(_ROOT_STORE_GUARD),
                provider.GetService<object>(_ACTION_TYPE_UNIQUENESS_CHECK));

            return rootModule;
        });

        services.AddRange(StoreConfigUtils.ProvideStore(reducers, config));
        return services;
    }

    public static IServiceCollection AddStoreFeature<T, V>(this IServiceCollection services,
        string featureName,
        ActionReducerMap<T, V>? reducers = null,
        StoreConfig<T, V>? config = null)
        where V : IAction
    {
        services.AddSingleton(provider =>
        {
            var featureModule = new StoreFeatureModule(
                provider.GetRequiredService<StoreFeature<object, IAction>[]>(),
                provider.GetRequiredService<ActionReducerMap<object, IAction>[]>(),
                provider.GetRequiredService<ReducerManager>(),
                provider.GetRequiredService<StoreRootModule>(),
                provider.GetService<object>(_ACTION_TYPE_UNIQUENESS_CHECK));

            return featureModule;
        });

        services.AddRange(StoreConfigUtils.ProvideState(featureName, reducers, config));
        return services;
    }

    public static IServiceCollection AddStoreFeature<T, V>(
        this IServiceCollection services,
        FeatureSlice<T, V> slice)
        where V : IAction
    {
        services.AddSingleton(provider =>
        {
            var featureModule = new StoreFeatureModule(
                new[] { slice.ToStoreFeature() },
                provider.GetRequiredService<ActionReducerMap<object, IAction>[]>(),
                provider.GetRequiredService<ReducerManager>(),
                provider.GetRequiredService<StoreRootModule>(),
                provider.GetService<object>(_ACTION_TYPE_UNIQUENESS_CHECK));

            return featureModule;
        });

        services.AddRange(StoreConfigUtils.ProvideState(slice));
        return services;
    }

    private static IEnumerable<ServiceDescriptor> ProvideStore<T, V>(
        ActionReducerMap<T, V> reducers,
        RootStoreConfig<T, V> config)
        where V : IAction
    {
        var descriptors = new List<ServiceDescriptor>();

        // Add services for the root store configuration
        // ...

        return descriptors;
    }

    private static IEnumerable<ServiceDescriptor> ProvideState<T, V>(string featureName,
        ActionReducerMap<T, V> reducers, StoreConfig<T, V> config) where V : IAction
    {
        var descriptors = new List<ServiceDescriptor>();

        // Add services for the feature state configuration
        // ...

        return descriptors;
    }

    private static IEnumerable<ServiceDescriptor> ProvideState<T, V>(FeatureSlice<T, V> slice) where V : IAction
    {
        var descriptors = new List<ServiceDescriptor>();

        // Add services for the feature slice configuration
        // ...

        return descriptors;
    }
}

public static class ServiceProviderExtensions
{
    public static T GetService<T>(this IServiceProvider provider, string token)
    {
        // Implement logic to get service by token
        return default;
    }
}