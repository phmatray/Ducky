using Bustand.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bustand.Before;

public static class Registration
{
    public static IServiceCollection AddApp(IServiceCollection services, IUserService users)
    {
        #region before-registration
        // Bustand: Scrutor scans for [BustandStore] classes; the store's services come from DI.
        services.AddSingleton(users);
        services.AddBustand(options => options.ScanAssemblyContaining<UserStore>());
        #endregion
        return services;
    }
}
