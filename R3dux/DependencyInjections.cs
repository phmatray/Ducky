using Microsoft.Extensions.DependencyInjection;
using R3;

namespace R3dux;

public static class DependencyInjections
{
    public static IServiceCollection AddR3dux(this IServiceCollection services)
    {
        services.AddBlazorR3();
        services.AddSingleton<IDispatcher, Dispatcher>();
        
        return services;
    }
}