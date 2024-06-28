using Microsoft.Extensions.DependencyInjection;
using R3;

namespace R3dux;

public static class Extensions
{
    public static IServiceCollection AddR3dux(this IServiceCollection services)
    {
        services.AddBlazorR3();
        
        return services;
    }
}