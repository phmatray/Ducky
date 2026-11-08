using Microsoft.Extensions.DependencyInjection;
using TimeWarp.State;

namespace TimeWarpSample.Before;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, ICounterApi api)
    {
        #region before-registration
        // TimeWarp.State: scans the assemblies for states and registers MediatR with them.
        services.AddSingleton(api);
        services.AddTimeWarpState(options => options.Assemblies = [typeof(Registration).Assembly]);
        #endregion
        return services;
    }
}
