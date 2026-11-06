using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky1x.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services)
    {
        #region after-registration
        // Ducky 2.0: one builder; the generated AddDuckyGenerated_{Assembly}() replaces the assembly scan.
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_Ducky1x_After()
            .UseJson(AppJson.Default)
            .AddBlazor());
        #endregion
        return services;
    }

    #region after-read
    // Ducky 2.0: the store's current snapshot.
    public static int ReadCount(IStore store) => store.State.Get<CounterState>().Value;
    #endregion
}
