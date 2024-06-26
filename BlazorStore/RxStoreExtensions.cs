using Microsoft.Extensions.DependencyInjection;

namespace BlazorStore;

public static class RxStoreExtensions
{
    public static IServiceCollection AddRxStore<TState, TReducer>(
        this IServiceCollection services)
        where TState : class
        where TReducer : IReducer<TState>, new()
    {
        services.AddSingleton(new RxStore<TState, TReducer>());
        
        return services;
    }
}