using Microsoft.Extensions.DependencyInjection;

namespace BlazorStore;

public static class RxStoreExtensions
{
    public static IServiceCollection AddBlazorStore(this IServiceCollection services)
    {
        services.AddSingleton<ActionsSubject>();
        
        return services;
    }
    
    public static IServiceCollection AddRxStore<TState, TReducer>(
        this IServiceCollection services)
        where TState : class
        where TReducer : IActionReducer<TState>, new()
    {
        services.AddSingleton<ActionsSubject>();

        services.AddSingleton<RxStore<TState, TReducer>>(
            provider => new RxStore<TState, TReducer>(provider.GetRequiredService<ActionsSubject>()));

        services.AddSingleton(provider => provider.GetRequiredService<RxStore<TState, TReducer>>().State);
        services.AddSingleton(provider => provider.GetRequiredService<RxStore<TState, TReducer>>().Actions);

        return services;
    }
}