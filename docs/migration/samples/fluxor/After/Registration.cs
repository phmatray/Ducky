using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace FluxorSample.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, ICounterApi api)
    {
        #region after-registration
        // Ducky 2.0: the generated AddDuckyGenerated_{Assembly}() replaces the reflection scan (slices and effects);
        // middleware is added with Use<T>(). Prerender<T> carries the prerendered slice into the interactive store.
        services.AddSingleton(api);
        services.AddSingleton<ActionLog>();
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_FluxorSample_After()
            .UseJson(AppJson.Default)
            .Use<ActionLogMiddleware>()
            .Prerender<CounterSlice, CounterState>(static state => state is { IsLoading: false, Error: null }));
        #endregion
        return services;
    }
}
