using BlazorAppRxStore.SeedWork;

namespace BlazorAppRxStore.Store;

public record AppState
{
    public int Count { get; init; } = 0;
    public string Message { get; init; } = "Hello, Blazor!";
}

public record SetMessage(string Message) : IAction;

public class AppReducer : ReducerBase<AppState>
{
    public AppReducer()
    {
        Register<SetMessage>((state, action)
            => state with { Message = action.Message });
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddRxStore(this IServiceCollection services)
    {
        var appReducer = new AppReducer();
        services.AddSingleton(new RxStore<AppState>(appReducer.Reduce));

        var counterReducer = new CounterReducer();
        services.AddSingleton(new RxStore<CounterState>(counterReducer.Reduce));

        return services;
    }
}