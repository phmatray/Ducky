// using Microsoft.Extensions.DependencyInjection;
//
// namespace Ducky.Blazor.Middlewares.DevTools;
//
// /// <summary>
// /// Provides extension methods for registering the Redux DevTools middleware and related services.
// /// </summary>
// public static class DevToolsServiceCollectionExtensions
// {
//     /// <summary>
//     /// Registers the Redux DevTools middleware and its dependencies for the given state type.
//     /// </summary>
//     /// <typeparam name="TState">The type of the state to log.</typeparam>
//     /// <param name="services">The service collection.</param>
//     /// <returns>The service collection for chaining.</returns>
//     public static IServiceCollection AddDevToolsMiddleware<TState>(this IServiceCollection services)
//         where TState : class
//     {
//         services.TryAddScoped<ReduxDevToolsModule<TState>>();
//         services.TryAddScoped<DevToolsMiddleware<TState>>();
//         return services;
//     }
// }
