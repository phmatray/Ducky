using Microsoft.Extensions.DependencyInjection;

namespace BlazorStore;

public static class StoreExtensions
{
    public static IServiceCollection AddBlazorStore(this IServiceCollection services)
    {
        services.AddSingleton<ActionsSubject>();
        
        return services;
    }

    public static IServiceCollection AddRxStore<TState, TReducer, TReducerFactory>(
        this IServiceCollection services)
        where TReducer : ActionReducer<TState>
        where TReducerFactory : IActionReducerFactory<TState>
        where TState : new()
    {
        services.AddSingleton<ReducerManager<TState>>(
            provider => new ReducerManager<TState>(
                provider.GetRequiredService<ActionsSubject>(),
                new TState(),
                new Dictionary<string, IActionReducer<TState>>
                {
                    { typeof(TReducer).Name, ActivatorUtilities.CreateInstance<TReducer>(provider) }
                },
                ActivatorUtilities.CreateInstance<TReducerFactory>(provider)));

        services.AddSingleton<State<TState>>(
            provider => new State<TState>(
                provider.GetRequiredService<ActionsSubject>(),
                provider.GetRequiredService<ReducerManager<TState>>().Reducers,
                new TState()));

        services.AddSingleton<Store<TState>>(
            provider => new Store<TState>(
                provider.GetRequiredService<State<TState>>(),
                provider.GetRequiredService<ActionsSubject>(),
                provider.GetRequiredService<ReducerManager<TState>>()));

        return services;
    }
}