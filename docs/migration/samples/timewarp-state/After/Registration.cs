using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace TimeWarpSample.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, ICounterApi api)
    {
        #region after-registration
        // Ducky 2.0: the generated AddDuckyGenerated_{Assembly}() registers the slice and the effect; no MediatR, no scan.
        services.AddSingleton(api);
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_TimeWarpSample_After()
            .UseJson(AppJson.Default)
            .AddBlazor());
        #endregion
        return services;
    }
}
