using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Bustand.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, IUserService users)
    {
        #region after-registration
        // Ducky 2.0: the generated AddDuckyGenerated_{Assembly}() registers UserStore and LoadUser; no circuit handler.
        services.AddSingleton(users);
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_Bustand_After()
            .AddBlazor());
        #endregion
        return services;
    }
}
