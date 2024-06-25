using BlazorAppRxStore.SeedWork;

namespace BlazorAppRxStore.Store;

public record AppState
{
    public int Count { get; set; }
    public string Message { get; set; }
}

public record SetMessage(string Message) : IAction;

public static class Reducers
{
    public static AppState RootReducer(AppState state, IAction action)
    {
        return action switch
        {
            SetMessage setMessage => state with { Message = setMessage.Message },
            _ => state
        };
    }
    
    public static CounterState CounterReducer(CounterState state, IAction action)
    {
        return action switch
        {
            Reset => new CounterState { Count = 0 },
            Increment => new CounterState { Count = state.Count + 1 },
            Decrement => new CounterState { Count = state.Count - 1 },
            _ => state
        };
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddRxStore(this IServiceCollection services)
    {
        services.AddSingleton(new RxStore<AppState>(
            new AppState { Count = 0, Message = "Hello, Blazor!" },
            Reducers.RootReducer));

        services.AddSingleton(new RxStore<CounterState>(
            new CounterState { Count = 10 },
            Reducers.CounterReducer));

        return services;
    }
}