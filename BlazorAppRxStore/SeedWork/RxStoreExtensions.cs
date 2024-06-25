namespace BlazorAppRxStore.SeedWork;

public static class RxStoreExtensions
{
    public static IServiceCollection AddRxStore<TState, TReducer>(
        this IServiceCollection services)
        where TState : class
        where TReducer : ReducerBase<TState>, new()
    {
        services.AddSingleton(new RxStore<TState, TReducer>());
        
        return services;
    }
}