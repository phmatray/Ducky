using Ducky;
using Ducky.Blazor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky1x.Before;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, IConfiguration configuration)
    {
        #region before-registration
        // Ducky 1.x: options bound from the "Ducky" configuration section; slices and effects found by assembly scan.
        services.AddDucky(configuration, options => options.AssemblyNames = ["Ducky1x.Before"]);
        #endregion
        return services;
    }

    #region before-read
    // Ducky 1.x: the root state, read through the store's observable.
    public static int ReadCount(DuckyStore store) =>
        store.RootStateObservable.CurrentValue.GetSliceState<CounterState>().Value;
    #endregion
}
