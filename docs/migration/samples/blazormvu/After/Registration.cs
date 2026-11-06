using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMvu.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services)
    {
        #region after-registration
        // Ducky 2.0: the slice and both effects come from the generated registration.
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_BlazorMvu_After()
            .AddBlazor());
        #endregion
        return services;
    }
}
