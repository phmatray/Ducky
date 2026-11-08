using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace StateContainerSample.Before;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, ICounterApi api)
    {
        #region before-registration
        // Scoped on the server (one per circuit); Microsoft's docs register it as a singleton in a standalone
        // WebAssembly app, and as a singleton in the .Client project's Program.cs for InteractiveAuto and
        // InteractiveWebAssembly. RegisterPersistentService hands its [PersistentState] properties from prerender to
        // interactive.
        services.AddSingleton(api);
        services.AddScoped<CounterStore>();
        services.AddRazorComponents()
            .RegisterPersistentService<CounterStore>(RenderMode.InteractiveAuto);
        #endregion
        return services;
    }
}
