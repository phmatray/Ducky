using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace TheBlazorState.After;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services)
    {
        #region after-registration
        // Ducky 2.0: one builder. Scope replaces KeySuffix and is resolved again when the user changes; Prerender replaces
        // the PersistentComponentState bridge.
        services.AddDucky(ducky => ducky
            .AddDuckyGenerated_TheBlazorState_After()
            .UseJson(AppJson.Default)
            .AddBlazor(o => o.Scope = (provider, _) => ValueTask.FromResult<string?>(provider.GetRequiredService<ICurrentUser>().Id))
            .Persist<DashboardSlice>(o => o.MaxAge = TimeSpan.FromMinutes(5))
            .Prerender<DashboardSlice>());
        #endregion
        return services;
    }
}
