using Microsoft.Extensions.DependencyInjection;
using TheBlazorState.Extensions;
using TheBlazorState.Storage;

namespace TheBlazorState.Before;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services)
    {
        #region before-registration
        // TheBlazorState: the library's services with [Persist] properties in localStorage (the prerender handoff always
        // runs too), then each [Shared] POCO.
        services.AddTheBlazorState(o => o.DefaultStorage = StorageStrategy.LocalStorage());
        services.AddScoped<AppState>();
        #endregion
        return services;
    }
}
