using Fluxor;
using Microsoft.Extensions.DependencyInjection;

namespace FluxorSample.Before;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, ICounterApi api)
    {
        #region before-registration
        // Fluxor: the reflection scan finds the feature, reducers and effects; middleware is added by hand. In a Blazor Web
        // App this runs in both Program.cs files.
        services.AddSingleton(api);
        services.AddSingleton<ActionLog>();
        services.AddFluxor(options => options
            .ScanAssemblies(typeof(Registration).Assembly)
            .AddMiddleware<ActionLogMiddleware>());
        #endregion
        return services;
    }
}
