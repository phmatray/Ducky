using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace StateContainerSample.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, ICounterApi api)
    {
        #region after-registration
        // Ducky 2.0: the store picks its lifetime (a singleton in the browser, scoped on the server). Prerender<T> replaces
        // [PersistentState] and RegisterPersistentService: it seeds the slice once the prerender store is idle, and only a
        // loaded state, so a failed prerender load is retried in the browser.
        services.AddSingleton(api);
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_StateContainerSample_After()
            .UseJson(AppJson.Default)
            .Prerender<CounterStore, CounterState>(static state => state is { IsLoading: false, Error: null }));
        #endregion
        return services;
    }
}
